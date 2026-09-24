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

    private sealed record PersonalRecordSource(
        Guid ExerciseId,
        string Exercise,
        decimal ValueKg,
        DateTime AchievedAt);
}
