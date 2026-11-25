using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Analytics;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<E1RmTrendPointDto>> GetE1rmTrendAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        return await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && record.ExerciseId == exerciseId)
            .OrderBy(record => record.RecordedAt)
            .Select(record => new E1RmTrendPointDto
            {
                ValueKg = record.ValueKg,
                RecordedAt = record.RecordedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PersonalRecordDto>> GetPersonalRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bodyweightKg = await BodyweightPortionResolver.LoadBodyweightAsync(_db, userId, cancellationToken);
        var shareByExerciseId = await _db.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.BodyweightShare > 0)
            .Select(exercise => new { exercise.Id, exercise.BodyweightShare })
            .ToDictionaryAsync(
                exercise => exercise.Id,
                exercise => BodyweightLoad.PortionKg(bodyweightKg, exercise.BodyweightShare),
                cancellationToken);

        var e1RmRecords = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .Select(record => new PersonalRecordSource(
                record.ExerciseId,
                record.Exercise.Name,
                record.ValueKg,
                record.RecordedAt))
            .ToListAsync(cancellationToken);

        var bestE1RmByExerciseId = e1RmRecords
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(record => record.ValueKg)
                    .ThenByDescending(record => record.AchievedAt)
                    .First());

        // Najteža serija se meri UKUPNIM opterećenjem, kao i e1RM iznad: zgib bez pojasa je
        // inače imao rekord „0 kg" pored procene maksimuma od 120. Snimak iz serije, a ne
        // trenutna masa — rekord je ono što je tada podignuto.
        var weightRecords = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new PersonalRecordSource(
                set.ExercisePlan.ExerciseId,
                set.ExercisePlan.Exercise.Name,
                set.WeightKg + set.BodyweightLoadKg,
                set.PerformedAt))
            .ToListAsync(cancellationToken);

        var bestWeightByExerciseId = weightRecords
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(record => record.ValueKg)
                    .ThenByDescending(record => record.AchievedAt)
                    .First());

        return bestE1RmByExerciseId.Keys
            .Union(bestWeightByExerciseId.Keys)
            .Select(exerciseId =>
            {
                bestE1RmByExerciseId.TryGetValue(exerciseId, out var bestE1Rm);
                bestWeightByExerciseId.TryGetValue(exerciseId, out var bestWeight);

                var achievedAt = new[] { bestE1Rm?.AchievedAt, bestWeight?.AchievedAt }
                    .Where(date => date.HasValue)
                    .Max();

                return new PersonalRecordDto
                {
                    ExerciseId = exerciseId,
                    Exercise = bestE1Rm?.Exercise ?? bestWeight?.Exercise ?? string.Empty,
                    BestE1Rm = bestE1Rm?.ValueKg,
                    BestWeight = bestWeight?.ValueKg,
                    AchievedAt = achievedAt,
                    IsBodyweight = shareByExerciseId.ContainsKey(exerciseId)
                };
            })
            .OrderBy(record => record.Exercise)
            .ToList();
    }

    public async Task<IReadOnlyList<WeeklyTonnageDto>> GetWeeklyTonnageAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default)
    {
        var mesocycleExists = await _db.Mesocycles.AnyAsync(
            mesocycle => mesocycle.Id == mesocycleId && mesocycle.UserId == userId,
            cancellationToken);

        if (!mesocycleExists)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Mesocycle was not found.");
        }

        var tonnageByWeek = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId)
            .GroupBy(set => set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber)
            .Select(group => new
            {
                WeekNumber = group.Key,
                // Tonaža ide nad ukupnim opterećenjem: trening od 40 zgibova je posao, a
                // sabirao se kao nula.
                TonnageKg = group.Sum(set => (set.WeightKg + set.BodyweightLoadKg) * set.Reps)
            })
            .ToDictionaryAsync(item => item.WeekNumber, item => item.TonnageKg, cancellationToken);

        var weeks = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId)
            .OrderBy(week => week.WeekNumber)
            .Select(week => new { week.WeekNumber, week.IsDeload })
            .ToListAsync(cancellationToken);

        return weeks
            .Select(week => new WeeklyTonnageDto
            {
                WeekNumber = week.WeekNumber,
                IsDeload = week.IsDeload,
                TonnageKg = tonnageByWeek.GetValueOrDefault(week.WeekNumber)
            })
            .ToList();
    }

    public async Task<TrendDto> GetTrendAsync(
        Guid userId,
        int? weeks,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        DateTime? since = weeks is null
            ? null
            : WeeklyTrend.WeekStartOf(now).AddDays(-7 * (Math.Max(1, weeks.Value) - 1));

        // Serije bez mišića, pa mišići posebno: spajanje preko SelectMany se ne prevodi na SQLite
        // (APPLY), a jedan upit manje po seriji je ionako jeftiniji nego jedan red po mišiću.
        var rows = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                          && (since == null || set.PerformedAt >= since))
            .Select(set => new
            {
                set.PerformedAt,
                set.WeightKg,
                set.BodyweightLoadKg,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.ExercisePlan.ExerciseId,
                BlockId = set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId,
                BlockName = set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.Name,
                set.ExercisePlan.WorkoutSession.TrainingWeek.IsDeload
            })
            .ToListAsync(cancellationToken);

        var exerciseIds = rows.Select(row => row.ExerciseId).Distinct().ToList();
        var musclesByExercise = (await _db.ExerciseMuscles
                .AsNoTracking()
                .Where(link => exerciseIds.Contains(link.ExerciseId))
                .Select(link => new { link.ExerciseId, link.MuscleGroupId, link.Contribution })
                .ToListAsync(cancellationToken))
            .GroupBy(link => link.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TrendMuscle>)group
                    .Select(link => new TrendMuscle(link.MuscleGroupId, link.Contribution))
                    .ToList());

        var planNameByBlock = (await _db.MacrocycleBlocks
                .AsNoTracking()
                .Where(block => block.MesocycleId != null && block.Macrocycle.UserId == userId)
                .Select(block => new { MesocycleId = block.MesocycleId!.Value, block.Macrocycle.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.MesocycleId, row => row.Name);

        var sets = rows
            .Select(row => new TrendSet(
                row.PerformedAt,
                row.WeightKg + row.BodyweightLoadKg,
                row.Reps,
                row.Rir,
                row.IsFailure,
                row.BlockId,
                row.BlockName,
                planNameByBlock.GetValueOrDefault(row.BlockId, row.BlockName),
                row.IsDeload,
                musclesByExercise.GetValueOrDefault(row.ExerciseId, [])))
            .ToList();

        var trend = WeeklyTrend.Build(sets, now, weeks);

        var muscleNames = await _db.MuscleGroups
            .AsNoTracking()
            .ToDictionaryAsync(group => group.Id, group => group.Name, cancellationToken);

        var trained = trend
            .SelectMany(week => week.StimulativeSetsByMuscle.Keys)
            .Distinct()
            .Select(id => muscleNames.GetValueOrDefault(id, string.Empty))
            .Where(name => name.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new TrendDto
        {
            Muscles = trained,
            Weeks = trend
                .Select(week => new TrendWeekDto
                {
                    WeekStart = week.WeekStart,
                    TonnageKg = week.TonnageKg,
                    Sets = week.Sets,
                    StimulativeSets = week.StimulativeSets,
                    StimulativeSetsByMuscle = week.StimulativeSetsByMuscle
                        .Where(entry => muscleNames.ContainsKey(entry.Key))
                        .ToDictionary(entry => muscleNames[entry.Key], entry => entry.Value),
                    BlockName = week.BlockName,
                    PlanName = week.PlanName,
                    IsDeload = week.IsDeload,
                    BlockStarts = week.BlockStarts
                })
                .ToList()
        };
    }

    private sealed record PersonalRecordSource(
        Guid ExerciseId,
        string Exercise,
        decimal ValueKg,
        DateTime AchievedAt);
}
