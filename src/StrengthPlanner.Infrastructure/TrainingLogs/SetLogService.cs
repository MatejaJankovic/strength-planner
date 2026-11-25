using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

public class SetLogService : ISetLogService
{
    private readonly AppDbContext _db;

    public SetLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SetLogDto> AddSetAsync(
        Guid userId,
        Guid exercisePlanId,
        AddSetLogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetInput(request.WeightKg, request.Reps, request.Rir, request.IsFailure);

        var planInfo = await _db.ExercisePlans
            .Where(plan => plan.Id == exercisePlanId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(plan => new
            {
                SessionId = plan.WorkoutSessionId,
                plan.WorkoutSession.Status,
                plan.RepRangeMin,
                plan.Exercise.BodyweightShare
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (planInfo is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise plan was not found.");
        }

        var correction = await EnsureSessionIsEditableAsync(
            userId,
            planInfo.SessionId,
            planInfo.Status,
            cancellationToken);

        var lastSetNumber = await _db.SetLogs
            .Where(set => set.ExercisePlanId == exercisePlanId)
            .Select(set => (int?)set.SetNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var isFailure = WorkingSet.ImpliesFailure(
            request.Reps, request.Rir, planInfo.RepRangeMin, request.IsFailure);

        // Deo telesne mase se SNIMA pri upisu, a ne računa kasnije: kilogram promenjen u
        // profilu ne sme da menja e1RM, tonažu i umor iz serija koje su već odrađene.
        var bodyweightLoadKg = planInfo.BodyweightShare <= 0
            ? 0m
            : BodyweightLoad.PortionKg(
                await _db.Profiles
                    .AsNoTracking()
                    .Where(profile => profile.UserId == userId)
                    .Select(profile => (decimal?)profile.BodyweightKg)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0m,
                planInfo.BodyweightShare);

        var setLog = new SetLog
        {
            Id = Guid.NewGuid(),
            ExercisePlanId = exercisePlanId,
            SetNumber = lastSetNumber + 1,
            WeightKg = request.WeightKg,
            Reps = request.Reps,
            // Rir stays exactly as sent: ValidateSetInput already rejects an explicit
            // otkaz with Rir > 0, and the implied case in ImpliesFailure only fires when
            // Rir is already 0, so there is never a request.Rir to normalize away here.
            Rir = request.Rir,
            IsFailure = isFailure,
            BodyweightLoadKg = bodyweightLoadKg,
            PerformedAt = DateTime.UtcNow
        };

        _db.SetLogs.Add(setLog);
        await _db.SaveChangesAsync(cancellationToken);

        if (correction == CorrectionMode.Recalculate)
        {
            await MarkRevisionPendingAsync(userId, planInfo.SessionId, cancellationToken);
        }

        return ToDto(setLog);
    }

    public async Task<SetLogDto> UpdateSetAsync(
        Guid userId,
        Guid setLogId,
        UpdateSetLogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetInput(request.WeightKg, request.Reps, request.Rir, request.IsFailure);

        var setLog = await _db.SetLogs
            .Include(set => set.ExercisePlan.WorkoutSession)
            .FirstOrDefaultAsync(
                set => set.Id == setLogId
                       && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (setLog is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Set log was not found.");
        }

        var correction = await EnsureSessionIsEditableAsync(
            userId,
            setLog.ExercisePlan.WorkoutSessionId,
            setLog.ExercisePlan.WorkoutSession.Status,
            cancellationToken);

        var isFailure = WorkingSet.ImpliesFailure(
            request.Reps, request.Rir, setLog.ExercisePlan.RepRangeMin, request.IsFailure);

        // BodyweightLoadKg se namerno ne dira: izmena ispravlja ponavljanja, RIR ili dodatu
        // težinu, a telesna masa tog dana je ostala ista.
        setLog.WeightKg = request.WeightKg;
        setLog.Reps = request.Reps;
        setLog.Rir = request.Rir;
        setLog.IsFailure = isFailure;

        if (correction == CorrectionMode.Recalculate)
        {
            setLog.ExercisePlan.WorkoutSession.RevisionPending = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(setLog);
    }

    public async Task DeleteSetAsync(
        Guid userId,
        Guid setLogId,
        CancellationToken cancellationToken = default)
    {
        var setLog = await _db.SetLogs
            .Include(set => set.ExercisePlan.WorkoutSession)
            .FirstOrDefaultAsync(
                set => set.Id == setLogId
                       && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (setLog is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Set log was not found.");
        }

        var correction = await EnsureSessionIsEditableAsync(
            userId,
            setLog.ExercisePlan.WorkoutSessionId,
            setLog.ExercisePlan.WorkoutSession.Status,
            cancellationToken);

        if (correction == CorrectionMode.Recalculate)
        {
            setLog.ExercisePlan.WorkoutSession.RevisionPending = true;
        }

        _db.SetLogs.Remove(setLog);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Serije se ne mogu menjati kad od sesije više ništa ne preostaje.
    ///
    /// Završena: progresija je već izračunata i naknadne izmene bi narušile istoriju.
    /// Preskočena: nedelja je na osnovu nje već zatvorena — ocenjena za umor, a možda je i
    /// sledeći blok plana generisan. Serija upisana posle toga bi bila rad koji nijedan
    /// obračun nije video. Trening se prvo vraća na plan, pa se loguje.
    /// </summary>
    /// <summary>
    /// A finished workout accepts sets only while it can still be corrected (see
    /// <see cref="SessionCorrection"/>). Returns how the correction will be applied, or null
    /// when the session is not a finished one.
    /// </summary>
    private async Task<CorrectionMode?> EnsureSessionIsEditableAsync(
        Guid userId,
        Guid sessionId,
        SessionStatus status,
        CancellationToken cancellationToken)
    {
        if (status != SessionStatus.Completed)
        {
            EnsureSessionIsEditable(status);
            return null;
        }

        var window = await SessionCorrectionResolver.ResolveAsync(_db, userId, sessionId, cancellationToken);
        if (window is { IsOpen: true })
        {
            return window.Mode;
        }

        throw new TrainingLogException(
            TrainingLogErrorType.Conflict,
            window?.BlockedBy switch
            {
                CorrectionBlock.NextWorkoutStarted =>
                    "The same day of the next week has already been started, so this workout can no longer be corrected.",
                CorrectionBlock.NextBlockOpened =>
                    "The next block was generated from this workout, so it can no longer be corrected.",
                _ => "Completed workout sessions cannot be modified."
            });
    }

    /// <summary>
    /// A set was added to a finished workout: what finishing it produced no longer describes
    /// its sets until the correction is saved.
    /// </summary>
    private async Task MarkRevisionPendingAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await _db.WorkoutSessions
            .Where(session => session.Id == sessionId && session.TrainingWeek.Mesocycle.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevisionPending, true),
                cancellationToken);
    }

    private static void EnsureSessionIsEditable(SessionStatus status)
    {
        if (!SessionLifecycle.IsSettled(status))
        {
            return;
        }

        throw new TrainingLogException(
            TrainingLogErrorType.Conflict,
            status == SessionStatus.Skipped
                ? "A skipped workout must be put back on the plan before sets can be logged."
                : "Completed workout sessions cannot be modified.");
    }

    private static void ValidateSetInput(decimal weightKg, int reps, int rir, bool isFailure)
    {
        if (weightKg < 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Weight must be greater than or equal to zero.");
        }

        if (reps <= 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Reps must be greater than zero.");
        }

        if (rir is < 0 or > 5)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "RIR must be between 0 and 5.");
        }

        if (isFailure && rir > 0)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "A set taken to failure cannot have reps in reserve.");
        }
    }

    private static SetLogDto ToDto(SetLog setLog)
    {
        return SetLogMapper.ToDto(setLog);
    }
}
