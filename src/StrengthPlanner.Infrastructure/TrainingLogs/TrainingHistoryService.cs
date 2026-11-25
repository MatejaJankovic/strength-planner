using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// The lifter's training history: every workout that is over, across all plans and blocks.
///
/// Analytics reads a block at a time and the plan screen shows the current block, so a block that
/// ended used to disappear from view. This is the one place that lists what was actually done.
/// </summary>
public class TrainingHistoryService : ITrainingHistoryService
{
    public const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public TrainingHistoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrainingHistoryDto> GetAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // The headers of every finished or skipped workout, without their sets: a few hundred
        // small rows, which is what lets the order be decided in one place. The date a workout
        // is sorted by is not always stored (see HistoryDate), so it cannot be a column.
        var headers = await _db.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.TrainingWeek.Mesocycle.UserId == userId
                              && SessionLifecycle.Settled.Contains(session.Status))
            .Select(session => new SessionHeader(
                session.Id,
                session.Status,
                session.DayLabel,
                session.Date,
                session.CompletedAt,
                session.TrainingWeek.WeekNumber,
                session.TrainingWeek.IsDeload,
                session.TrainingWeek.MesocycleId,
                session.TrainingWeek.Mesocycle.Name,
                session.TrainingWeek.Mesocycle.Goal,
                session.TrainingWeek.Mesocycle.StartDate))
            .ToListAsync(cancellationToken);

        var lastSetBySession = (await _db.SetLogs
                .AsNoTracking()
                // Samo treninzi bez vremena završetka se datiraju po poslednjoj seriji; ostalima
                // serije ne trebaju, pa se i ne čitaju.
                .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                              && set.ExercisePlan.WorkoutSession.CompletedAt == null)
                .GroupBy(set => set.ExercisePlan.WorkoutSessionId)
                .Select(group => new { SessionId = group.Key, Last = group.Max(set => set.PerformedAt) })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.SessionId, row => row.Last);

        var planNameByMesocycle = (await _db.MacrocycleBlocks
                .AsNoTracking()
                .Where(block => block.MesocycleId != null && block.Macrocycle.UserId == userId)
                .Select(block => new { MesocycleId = block.MesocycleId!.Value, block.Macrocycle.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.MesocycleId, row => row.Name);

        var now = DateTime.UtcNow;
        var ordered = headers
            .Select(header => new
            {
                Header = header,
                Date = HistoryDate.Of(
                    header.CompletedAt,
                    // A workout with no sets has no entry. GetValueOrDefault would give it the default
                    // DateTime, 0001-01-01, which is not "no date" and would sort as the oldest.
                    lastSetBySession.TryGetValue(header.Id, out var lastSet) ? lastSet : null,
                    header.Date,
                    header.MesocycleStart,
                    header.WeekNumber,
                    now)
            })
            .OrderByDescending(row => row.Date.Value)
            .ThenByDescending(row => row.Header.WeekNumber)
            .ThenBy(row => row.Header.DayLabel, StringComparer.Ordinal)
            // Poslednji ključ: dva treninga sa istim datumom, nedeljom i danom (npr. dva
            // preskočena u različitim blokovima) bi inače menjala mesto između stranica.
            .ThenBy(row => row.Header.Id)
            .ToList();

        // Pomeraj se računa u long: sa velikim brojem strane bi int prelio u negativan, a Skip
        // sa negativnim brojem vraća prvu stranicu umesto prazne.
        var skip = (long)(page - 1) * pageSize;
        var pageRows = ordered
            .Skip(skip > int.MaxValue ? int.MaxValue : (int)skip)
            .Take(pageSize)
            .ToList();

        var pageIds = pageRows.Select(row => row.Header.Id).ToList();
        var plans = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => pageIds.Contains(plan.WorkoutSessionId)
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .OrderBy(plan => plan.Order)
            .Select(plan => new
            {
                plan.WorkoutSessionId,
                ExerciseName = plan.Exercise.Name,
                Sets = plan.SetLogs
                    .Select(set => new { set.WeightKg, set.Reps, set.BodyweightLoadKg })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var plansBySession = plans.ToLookup(plan => plan.WorkoutSessionId);

        var items = pageRows
            .Select(row =>
            {
                var sessionPlans = plansBySession[row.Header.Id].ToList();
                var sets = sessionPlans.SelectMany(plan => plan.Sets).ToList();

                return new HistoryEntryDto
                {
                    SessionId = row.Header.Id,
                    Status = row.Header.Status.ToString(),
                    Date = row.Date.Value,
                    DateKnown = row.Date.Known,
                    DayLabel = row.Header.DayLabel,
                    WeekNumber = row.Header.WeekNumber,
                    IsDeload = row.Header.IsDeload,
                    PlanName = planNameByMesocycle.GetValueOrDefault(row.Header.MesocycleId, row.Header.BlockName),
                    BlockName = row.Header.BlockName,
                    Goal = row.Header.Goal.ToString(),
                    SetCount = sets.Count,
                    TonnageKg = sets.Sum(set => (set.WeightKg + set.BodyweightLoadKg) * set.Reps),
                    Exercises = sessionPlans
                        .Where(plan => plan.Sets.Count > 0)
                        .Select(plan => plan.ExerciseName)
                        .ToList()
                };
            })
            .ToList();

        return new TrainingHistoryDto
        {
            Items = items,
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    private sealed record SessionHeader(
        Guid Id,
        SessionStatus Status,
        string DayLabel,
        DateTime? Date,
        DateTime? CompletedAt,
        int WeekNumber,
        bool IsDeload,
        Guid MesocycleId,
        string BlockName,
        Goal Goal,
        DateTime MesocycleStart);
}
