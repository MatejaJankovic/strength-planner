using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Čita lične MEV/MRV granice i pomera ih posle svake završene nedelje.
/// Seed vrednosti iz <see cref="VolumeLandmark"/> ostaju referentna tačka: lične
/// granice su dozvoljene samo u pojasu oko njih i tu se vraćaju na reset.
/// </summary>
public sealed class VolumeLandmarkService
{
    private readonly AppDbContext _db;

    public VolumeLandmarkService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Efektivne granice po mišićnoj grupi: lične ako postoje, inače seed vrednosti.
    /// </summary>
    public async Task<Dictionary<Guid, EffectiveLandmark>> GetEffectiveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var seeds = await _db.VolumeLandmarks
            .AsNoTracking()
            .Select(landmark => new { landmark.MuscleGroupId, landmark.Mev, landmark.Mrv })
            .ToListAsync(cancellationToken);

        var personal = await _db.UserVolumeLandmarks
            .AsNoTracking()
            .Where(landmark => landmark.UserId == userId)
            .ToDictionaryAsync(landmark => landmark.MuscleGroupId, cancellationToken);

        return seeds.ToDictionary(
            seed => seed.MuscleGroupId,
            seed =>
            {
                if (personal.TryGetValue(seed.MuscleGroupId, out var own))
                {
                    return new EffectiveLandmark(
                        own.Mev,
                        own.Mrv,
                        seed.Mev,
                        seed.Mrv,
                        IsPersonal: own.Mev != seed.Mev || own.Mrv != seed.Mrv,
                        own.UpdatedAt);
                }

                return new EffectiveLandmark(seed.Mev, seed.Mrv, seed.Mev, seed.Mrv, IsPersonal: false, null);
            });
    }

    /// <summary>
    /// Pomera granice na osnovu upravo završene nedelje. Poziva se tačno jednom po
    /// nedelji — u trenutku kada poslednja sesija te nedelje pređe u Completed.
    /// Deload nedelje se preskaču: namerno su submaksimalne, pa o toleranciji na
    /// volumen ne govore ništa.
    /// </summary>
    public async Task AdaptAfterWeekAsync(
        Guid userId,
        Guid trainingWeekId,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var week = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(item => item.Id == trainingWeekId && item.Mesocycle.UserId == userId)
            .Select(item => new { item.Id, item.IsDeload })
            .FirstOrDefaultAsync(cancellationToken);

        if (week is null || week.IsDeload)
        {
            return;
        }

        var responses = await BuildWeeklyResponsesAsync(userId, trainingWeekId, cancellationToken);
        if (responses.Count == 0)
        {
            return;
        }

        var seeds = await _db.VolumeLandmarks
            .AsNoTracking()
            .Select(landmark => new { landmark.MuscleGroupId, landmark.Mev, landmark.Mrv })
            .ToDictionaryAsync(
                landmark => landmark.MuscleGroupId,
                landmark => new VolumeLandmarkValues(landmark.Mev, landmark.Mrv),
                cancellationToken);

        var personal = await _db.UserVolumeLandmarks
            .Where(landmark => landmark.UserId == userId)
            .ToDictionaryAsync(landmark => landmark.MuscleGroupId, cancellationToken);

        foreach (var (muscleGroupId, response) in responses)
        {
            if (!seeds.TryGetValue(muscleGroupId, out var seed))
            {
                continue;
            }

            personal.TryGetValue(muscleGroupId, out var row);
            var current = row is null
                ? seed
                : new VolumeLandmarkValues(row.Mev, row.Mrv);

            var adjusted = VolumeAdaptation.Adjust(current, seed, response);
            if (adjusted == current)
            {
                continue;
            }

            if (row is null)
            {
                _db.UserVolumeLandmarks.Add(new UserVolumeLandmark
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MuscleGroupId = muscleGroupId,
                    Mev = adjusted.Mev,
                    Mrv = adjusted.Mrv,
                    UpdatedAt = completedAt
                });
            }
            else
            {
                row.Mev = adjusted.Mev;
                row.Mrv = adjusted.Mrv;
                row.UpdatedAt = completedAt;
            }
        }
    }

    /// <summary>
    /// Vraća lične granice na seed vrednosti (briše redove).
    /// </summary>
    public async Task ResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await _db.UserVolumeLandmarks
            .Where(landmark => landmark.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.UserVolumeLandmarks.RemoveRange(rows);
    }

    /// <summary>
    /// Sažima nedelju u jedan odgovor po mišićnoj grupi. Sve se meri doprinosom serije
    /// (primarna 1.0, sekundarna 0.5), pa sekundarni rad ne broji kao pun.
    /// </summary>
    private async Task<Dictionary<Guid, VolumeResponse>> BuildWeeklyResponsesAsync(
        Guid userId,
        Guid trainingWeekId,
        CancellationToken cancellationToken)
    {
        var contributions = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == trainingWeekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .SelectMany(set => set.ExercisePlan.Exercise.Muscles.Select(muscle => new SetContribution(
                muscle.MuscleGroupId,
                muscle.Contribution,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin)))
            .ToListAsync(cancellationToken);

        return contributions
            .GroupBy(item => item.MuscleGroupId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var totalWeight = group.Sum(item => item.Contribution);
                    if (totalWeight == 0)
                    {
                        return new VolumeResponse(0, 0, 0);
                    }

                    var deviation = group.Sum(item =>
                        item.Contribution
                        * (new WorkingSet(item.Reps, item.Rir, item.IsFailure).EffectiveRir(item.RepRangeMin)
                           - item.TargetRir));
                    var failed = group
                        .Where(item => item.IsFailure)
                        .Sum(item => item.Contribution);

                    return new VolumeResponse(
                        totalWeight,
                        deviation / totalWeight,
                        failed / totalWeight);
                });
    }

    private sealed record SetContribution(
        Guid MuscleGroupId,
        decimal Contribution,
        int Reps,
        int Rir,
        bool IsFailure,
        int TargetRir,
        int RepRangeMin);
}

/// <summary>Granice koje važe za korisnika, uz seed vrednosti radi poređenja u UI-ju.</summary>
public sealed record EffectiveLandmark(
    int Mev,
    int Mrv,
    int SeedMev,
    int SeedMrv,
    bool IsPersonal,
    DateTime? UpdatedAt);
