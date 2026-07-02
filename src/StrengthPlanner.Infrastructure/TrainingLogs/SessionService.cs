using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

public class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly E1RmCalculator _e1RmCalculator = new();
    private readonly ProgressionEngine _progressionEngine = new();

    public SessionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkoutSessionDto> GetByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await BuildSessionDetailsQuery(userId)
            .FirstOrDefaultAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        return ToDto(session);
    }

    public async Task<WorkoutSessionDto> StartAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .FirstOrDefaultAsync(
                workoutSession => workoutSession.Id == sessionId
                                  && workoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        if (session.Status == SessionStatus.Completed)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Completed workout sessions cannot be started again.");
        }

        if (session.Status == SessionStatus.Planned)
        {
            session.Status = SessionStatus.InProgress;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var detailedSession = await BuildSessionDetailsQuery(userId)
            .FirstAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        return ToDto(detailedSession);
    }

    public async Task<CompleteSessionResultDto> CompleteAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

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

        if (session.Status == SessionStatus.Completed)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Workout session is already completed.");
        }

        var exerciseIds = session.ExercisePlans
            .Select(plan => plan.ExerciseId)
            .Distinct()
            .ToList();
        var previousMaxByExerciseId = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .GroupBy(record => record.ExerciseId)
            .Select(group => new { ExerciseId = group.Key, ValueKg = group.Max(record => record.ValueKg) })
            .ToDictionaryAsync(record => record.ExerciseId, record => record.ValueKg, cancellationToken);

        // Samo sesije koje još nisu završene: complete van redosleda ne sme da
        // prepiše ciljeve već odrađenih treninga.
        var nextPlans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
                .ThenInclude(workoutSession => workoutSession.TrainingWeek)
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == session.TrainingWeek.MesocycleId
                           && plan.WorkoutSession.DayLabel == session.DayLabel
                           && plan.WorkoutSession.TrainingWeek.WeekNumber > session.TrainingWeek.WeekNumber
                           && plan.WorkoutSession.Status != SessionStatus.Completed
                           && exerciseIds.Contains(plan.ExerciseId))
            .OrderBy(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
            .ThenBy(plan => plan.Order)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        session.Status = SessionStatus.Completed;

        var summaries = new List<CompletedExerciseSummaryDto>();

        foreach (var plan in session.ExercisePlans.OrderBy(plan => plan.Order))
        {
            var nextPlan = nextPlans.FirstOrDefault(candidate => candidate.ExerciseId == plan.ExerciseId);
            var logs = plan.SetLogs
                .OrderBy(set => set.SetNumber)
                .ToList();

            var summary = new CompletedExerciseSummaryDto
            {
                ExercisePlanId = plan.Id,
                ExerciseId = plan.ExerciseId,
                ExerciseName = plan.Exercise.Name
            };

            if (logs.Count == 0)
            {
                if (nextPlan is not null)
                {
                    nextPlan.TargetWeightKg = plan.TargetWeightKg;
                    summary.NextWeightKg = nextPlan.TargetWeightKg;
                }

                summaries.Add(summary);
                continue;
            }

            // Deload serije su namerno submaksimalne — njihov e1RM bi veštački
            // oborio trend snage i start sledećeg mezociklusa, pa se ne upisuje.
            var bestEstimate = session.TrainingWeek.IsDeload ? null : EstimateBestOneRepMax(logs);
            if (bestEstimate.HasValue)
            {
                summary.E1Rm = bestEstimate.Value;
                summary.IsPr = previousMaxByExerciseId.TryGetValue(plan.ExerciseId, out var previousMax)
                               && bestEstimate.Value > previousMax;

                _db.OneRepMaxRecords.Add(new OneRepMaxRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ExerciseId = plan.ExerciseId,
                    ValueKg = bestEstimate.Value,
                    Source = OneRepMaxSource.Estimated,
                    RecordedAt = now
                });
            }

            // Progresija polazi od težine koju je korisnik STVARNO koristio;
            // planska težina je samo fallback (logova ovde uvek ima).
            var usedWeight = logs.Count > 0
                ? logs.Average(set => set.WeightKg)
                : plan.TargetWeightKg ?? 0m;
            var workingSets = logs
                .Select(set => (set.Reps, set.Rir))
                .ToList();
            var progression = _progressionEngine.ComputeNext(
                usedWeight,
                workingSets,
                plan.TargetRir,
                plan.RepRangeMin,
                plan.RepRangeMax);

            summary.NextWeightKg = progression.NextWeightKg;
            summary.WeightIncreased = progression.WeightIncreased;

            if (nextPlan is not null)
            {
                // Deload = 90% STVARNE težine iz ove nedelje (bez +2.5 progresije).
                var nextWeight = nextPlan.WorkoutSession.TrainingWeek.IsDeload
                    ? WeightMath.RoundToStep(
                        usedWeight * TrainingConstants.DeloadWeightFactor,
                        TrainingConstants.WeightStepKg)
                    : progression.NextWeightKg;

                nextPlan.TargetWeightKg = nextWeight;
                summary.NextWeightKg = nextWeight;
            }

            summaries.Add(summary);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CompleteSessionResultDto
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Exercises = summaries
        };
    }

    private IQueryable<WorkoutSession> BuildSessionDetailsQuery(Guid userId)
    {
        return _db.WorkoutSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.Exercise)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.SetLogs)
            .Where(workoutSession => workoutSession.TrainingWeek.Mesocycle.UserId == userId);
    }

    private decimal? EstimateBestOneRepMax(IReadOnlyList<SetLog> logs)
    {
        decimal? bestEstimate = null;

        foreach (var log in logs.Where(log => log.Reps <= TrainingConstants.EpleyRepCap))
        {
            var estimate = _e1RmCalculator.EstimateOneRepMax(log.WeightKg, log.Reps, log.Rir);
            if (!bestEstimate.HasValue || estimate > bestEstimate.Value)
            {
                bestEstimate = estimate;
            }
        }

        return bestEstimate;
    }

    private static WorkoutSessionDto ToDto(WorkoutSession session)
    {
        return new WorkoutSessionDto
        {
            Id = session.Id,
            WeekNumber = session.TrainingWeek.WeekNumber,
            IsDeload = session.TrainingWeek.IsDeload,
            DayLabel = session.DayLabel,
            Date = session.Date,
            Status = session.Status.ToString(),
            ExercisePlans = session.ExercisePlans
                .OrderBy(plan => plan.Order)
                .Select(plan => new ExercisePlanDto
                {
                    Id = plan.Id,
                    ExerciseId = plan.ExerciseId,
                    ExerciseName = plan.Exercise.Name,
                    Order = plan.Order,
                    TargetSets = plan.TargetSets,
                    RepRangeMin = plan.RepRangeMin,
                    RepRangeMax = plan.RepRangeMax,
                    TargetRir = plan.TargetRir,
                    TargetWeightKg = plan.TargetWeightKg,
                    SetLogs = plan.SetLogs
                        .OrderBy(set => set.SetNumber)
                        .Select(set => new SetLogDto
                        {
                            Id = set.Id,
                            ExercisePlanId = set.ExercisePlanId,
                            SetNumber = set.SetNumber,
                            WeightKg = set.WeightKg,
                            Reps = set.Reps,
                            Rir = set.Rir,
                            PerformedAt = set.PerformedAt
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
