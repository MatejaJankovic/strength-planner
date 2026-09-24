using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Where one training week's volume should land, per muscle group.
///
/// Exists as one class because two screens must not disagree about it: the set balancing
/// aims at these numbers and the weekly-volume screen shows them. The same shape of defect
/// as <c>SetLogDto</c> in round 9 — a value built by hand in two places, and the second one
/// left behind by the next change.
///
/// The week's share of the block is read rather than derived: the base week's prescription
/// is the block's base volume (see <see cref="Periodization.BaseWeekNumber"/>), and this
/// week's prescription divided by it is exactly how far periodization moved the work. A
/// flat block gives one, so its target stays MAV and nothing about existing blocks moves.
/// </summary>
public sealed class WeeklyVolumeTargetResolver
{
    private readonly AppDbContext _db;

    public WeeklyVolumeTargetResolver(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Targets for the week, or an empty result when the week is not one that aims at a
    /// volume target — a deload week aims at rest, and its halved sets are the point.
    /// </summary>
    public async Task<WeeklyVolumeTargets> ResolveAsync(
        Guid userId,
        Guid trainingWeekId,
        IReadOnlyDictionary<Guid, EffectiveLandmark> landmarks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(landmarks);

        var week = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(candidate => candidate.Id == trainingWeekId
                                && candidate.Mesocycle.UserId == userId)
            .Select(candidate => new
            {
                candidate.WeekNumber,
                candidate.IsDeload,
                candidate.MesocycleId,
                candidate.Mesocycle.Goal,
                candidate.Mesocycle.PeriodizationModel
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (week is null)
        {
            return WeeklyVolumeTargets.None;
        }

        var prescribed = await PrescribedVolumeAsync(userId, week.MesocycleId, week.WeekNumber, cancellationToken);

        // Osnovna nedelja je jedina iz koje se polazni volumen SME procitati: nijedna
        // verzija Periodization-a je nije pomerala. Kada je i ona postala deload, njen
        // propis je prepolovljen, pa odnos nema smisla - nedelja tada cilja kao osnovna.
        var baseWeekNumber = Periodization.BaseWeekNumber(week.PeriodizationModel);
        var baseVolume = baseWeekNumber == week.WeekNumber
            ? prescribed
            : await PrescribedVolumeAsync(userId, week.MesocycleId, baseWeekNumber, cancellationToken);

        var targets = new Dictionary<Guid, MuscleVolumeTarget>();

        foreach (var (muscleGroupId, landmark) in landmarks)
        {
            var values = new VolumeLandmarkValues(landmark.Mev, landmark.Mav, landmark.Mrv);

            targets[muscleGroupId] = new MuscleVolumeTarget(
                muscleGroupId,
                WeeklyVolumeTarget.ForWeek(
                    week.Goal,
                    values,
                    prescribed.GetValueOrDefault(muscleGroupId),
                    baseVolume.GetValueOrDefault(muscleGroupId)),
                landmark.Mrv);
        }

        return new WeeklyVolumeTargets(targets, week.IsDeload);
    }

    /// <summary>
    /// Volume one week prescribes per muscle group: every plan's <b>prescribed</b> sets
    /// weighted by how much of a set the exercise contributes.
    ///
    /// The prescription, not the proposal: the proposal is what balancing already moved,
    /// so reading it would make the target chase its own previous answer. Every session of
    /// the week counts, including the completed ones — the wave is a property of the week,
    /// not of the part of it still to come.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> PrescribedVolumeAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        var contributions = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                           && plan.WorkoutSession.TrainingWeek.WeekNumber == weekNumber
                           && !plan.WorkoutSession.TrainingWeek.IsDeload
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .SelectMany(plan => plan.Exercise.Muscles.Select(muscle => new
            {
                muscle.MuscleGroupId,
                Sets = muscle.Contribution * plan.PrescribedSets
            }))
            .ToListAsync(cancellationToken);

        return contributions
            .GroupBy(contribution => contribution.MuscleGroupId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Sets));
    }
}

/// <summary>
/// Weekly volume targets for one training week.
/// </summary>
/// <param name="ByMuscleGroupId">Target and ceiling per muscle group the system has limits for.</param>
/// <param name="IsDeloadWeek">True when the week aims at rest rather than at a volume target.</param>
public sealed record WeeklyVolumeTargets(
    IReadOnlyDictionary<Guid, MuscleVolumeTarget> ByMuscleGroupId,
    bool IsDeloadWeek)
{
    public static readonly WeeklyVolumeTargets None =
        new(new Dictionary<Guid, MuscleVolumeTarget>(), IsDeloadWeek: false);
}
