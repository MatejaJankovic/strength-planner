using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
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
            .Select(plan => new { plan.WorkoutSession.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (planInfo is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise plan was not found.");
        }

        EnsureSessionIsEditable(planInfo.Status);

        var lastSetNumber = await _db.SetLogs
            .Where(set => set.ExercisePlanId == exercisePlanId)
            .Select(set => (int?)set.SetNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var setLog = new SetLog
        {
            Id = Guid.NewGuid(),
            ExercisePlanId = exercisePlanId,
            SetNumber = lastSetNumber + 1,
            WeightKg = request.WeightKg,
            Reps = request.Reps,
            Rir = NormalizeRir(request.Rir, request.IsFailure),
            IsFailure = request.IsFailure,
            PerformedAt = DateTime.UtcNow
        };

        _db.SetLogs.Add(setLog);
        await _db.SaveChangesAsync(cancellationToken);

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

        EnsureSessionIsEditable(setLog.ExercisePlan.WorkoutSession.Status);

        setLog.WeightKg = request.WeightKg;
        setLog.Reps = request.Reps;
        setLog.Rir = NormalizeRir(request.Rir, request.IsFailure);
        setLog.IsFailure = request.IsFailure;

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

        EnsureSessionIsEditable(setLog.ExercisePlan.WorkoutSession.Status);

        _db.SetLogs.Remove(setLog);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Serije se ne mogu menjati kad je sesija završena — progresija je već
    /// izračunata i naknadne izmene bi narušile istoriju.
    /// </summary>
    private static void EnsureSessionIsEditable(SessionStatus status)
    {
        if (status == SessionStatus.Completed)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Completed workout sessions cannot be modified.");
        }
    }

    /// <summary>
    /// Otkaz i RIR > 0 se međusobno isključuju: ako je serija izvučena do otkaza,
    /// u rezervi po definiciji nije ostalo nijedno ponavljanje.
    /// </summary>
    private static int NormalizeRir(int rir, bool isFailure)
    {
        return isFailure ? 0 : rir;
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
        return new SetLogDto
        {
            Id = setLog.Id,
            ExercisePlanId = setLog.ExercisePlanId,
            SetNumber = setLog.SetNumber,
            WeightKg = setLog.WeightKg,
            Reps = setLog.Reps,
            Rir = setLog.Rir,
            IsFailure = setLog.IsFailure,
            PerformedAt = setLog.PerformedAt
        };
    }
}
