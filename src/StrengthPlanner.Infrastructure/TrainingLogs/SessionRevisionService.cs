using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Saves the correction of a finished workout.
///
/// The sets are changed one by one through <see cref="SetLogService"/>, which only marks the
/// workout as having a correction pending. This is where that correction is applied: what
/// finishing the workout wrote is taken back and written again from the corrected sets,
/// through the same steps finishing uses (<see cref="SessionOutcomes"/> and
/// <see cref="WeekConsequences"/>), so a corrected workout ends up exactly where one entered
/// correctly the first time would have.
///
/// It is idempotent: with nothing changed it writes the same values again.
/// </summary>
public class SessionRevisionService : ISessionRevisionService
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _volumeLandmarks;
    private readonly SessionOutcomes _outcomes;
    private readonly WeekConsequences _week;

    public SessionRevisionService(
        AppDbContext db,
        VolumeLandmarkService volumeLandmarks,
        DeloadService deloads,
        WeeklySetPlanner setPlanner)
    {
        _db = db;
        _volumeLandmarks = volumeLandmarks;
        _outcomes = new SessionOutcomes(db);
        _week = new WeekConsequences(volumeLandmarks, deloads, setPlanner);
    }

    public async Task<SessionRevisionResultDto> ReviseAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Called from inside another request's transaction (see ApplyPendingAsync), it joins
        // that transaction instead of opening a second one.
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var session = await _db.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.Exercise)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.SetLogs)
            .FirstOrDefaultAsync(
                workoutSession => workoutSession.Id == sessionId
                                  && workoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        var window = await SessionCorrectionResolver.ResolveAsync(_db, userId, sessionId, cancellationToken)
                     ?? throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");

        if (!window.IsOpen)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, ClosedMessage(window.BlockedBy));
        }

        if (window.Mode != CorrectionMode.Recalculate || session.CompletedAt is null)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "This workout was finished before its results were recorded per workout, so there is nothing to recalculate.");
        }

        var week = session.TrainingWeek;
        var mesocycleId = week.MesocycleId;
        var previousScore = week.FatigueScore;
        var now = DateTime.UtcNow;

        // 1. Take back what finishing the workout wrote: its estimate, its week's move of the
        //    volume limits, its week's fatigue score. Each of those writes ties back to this
        //    workout or to its week, which is why a correction is limited to workouts that
        //    were finished after that was recorded.
        await _db.OneRepMaxRecords
            .Where(record => record.WorkoutSessionId == sessionId && record.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (week.VolumeAdaptedAt is not null)
        {
            await _volumeLandmarks.UndoWeekAsync(userId, mesocycleId, week.Id, cancellationToken);
        }

        if (previousScore is not null)
        {
            await _db.TrainingWeeks
                .Where(candidate => candidate.Id == week.Id && candidate.Mesocycle.UserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(candidate => candidate.FatigueScore, (decimal?)null),
                    cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(week).ReloadAsync(cancellationToken);

        // 2. Write it again from the corrected sets, the way finishing does.
        var nextPlans = await _outcomes.LoadNextPlansAsync(userId, session, cancellationToken);
        var outcome = await _outcomes.ApplyAsync(userId, session, nextPlans, session.CompletedAt.Value, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var autoDeload = await _week.CloseAsync(userId, mesocycleId, now, cancellationToken);

        outcome.Settle();
        await _db.SaveChangesAsync(cancellationToken);

        var volumeAdjustments = await _week.RebalanceAsync(
            userId,
            mesocycleId,
            week.Mesocycle.SetAllocation,
            cancellationToken);

        session.RevisionPending = false;
        await _db.SaveChangesAsync(cancellationToken);

        var score = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(candidate => candidate.Id == week.Id)
            .Select(candidate => candidate.FatigueScore)
            .FirstOrDefaultAsync(cancellationToken);

        var deloadKept = autoDeload is null
                         && await DeloadStaysAsync(userId, mesocycleId, week.WeekNumber, previousScore, score, cancellationToken);

        if (ownsTransaction)
        {
            await transaction!.CommitAsync(cancellationToken);
        }

        return new SessionRevisionResultDto
        {
            Result = CompletionResults.Build(session, outcome, autoDeload, volumeAdjustments, nextBlock: null),
            PreviousFatigueScore = previousScore,
            FatigueScore = score,
            DeloadKept = deloadKept
        };
    }

    public async Task ApplyPendingAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _db.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.RevisionPending
                              && session.Status == SessionStatus.Completed
                              && session.TrainingWeek.MesocycleId == mesocycleId
                              && session.TrainingWeek.Mesocycle.UserId == userId)
            .OrderBy(session => session.TrainingWeek.WeekNumber)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in pending)
        {
            try
            {
                await ReviseAsync(userId, sessionId, cancellationToken);
            }
            catch (TrainingLogException error) when (error.ErrorType == TrainingLogErrorType.Conflict)
            {
                // The window closed without the correction being saved (a state from before
                // this was applied automatically). Nothing can be recalculated any more, so the
                // flag is cleared rather than left to offer a save that always fails.
                await _db.WorkoutSessions
                    .Where(session => session.Id == sessionId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(session => session.RevisionPending, false),
                        cancellationToken);
            }
        }
    }

    /// <summary>
    /// A deload that this week's score pulled forward stays when the corrected score no longer
    /// reaches the threshold. The next week was rewritten for it, and the lifter may already
    /// have seen that, so the correction reports it rather than quietly undoing it.
    /// </summary>
    private async Task<bool> DeloadStaysAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        decimal? previousScore,
        decimal? newScore,
        CancellationToken cancellationToken)
    {
        if (previousScore is null)
        {
            return false;
        }

        var threshold = ExperienceProgramming.DeloadThreshold(
            await BlockExperienceLevel.ForBlockAsync(_db, userId, mesocycleId, cancellationToken));

        if (threshold is null || previousScore < threshold)
        {
            return false;
        }

        var pulledForward = await _db.TrainingWeeks
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.MesocycleId == mesocycleId
                             && candidate.Mesocycle.UserId == userId
                             && candidate.IsAutoDeload
                             && candidate.WeekNumber == weekNumber + 1,
                cancellationToken);

        return pulledForward && (newScore is null || newScore < threshold);
    }

    private static string ClosedMessage(CorrectionBlock reason)
    {
        return reason switch
        {
            CorrectionBlock.NextWorkoutStarted =>
                "The same day of the next week has already been started, so this workout can no longer be corrected.",
            CorrectionBlock.NextBlockOpened =>
                "The next block was generated from this workout, so it can no longer be corrected.",
            _ => "Only a completed workout can be corrected."
        };
    }
}
