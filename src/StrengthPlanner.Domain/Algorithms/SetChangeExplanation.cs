using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>The muscle group, and the limit, that best explain one moved set proposal.</summary>
/// <param name="MuscleGroupId">Muscle group whose volume the move served.</param>
/// <param name="Reason">Whether the week's limits or one session's ceiling asked for it.</param>
public sealed record SetChangeCause(Guid MuscleGroupId, SetChangeReason Reason);

/// <summary>
/// A balanced week, as much of it as an explanation needs.
/// </summary>
/// <param name="Stimulative">Weekly stimulative volume per muscle after balancing, banked sets included.</param>
/// <param name="Raw">Weekly raw volume per muscle after balancing, banked sets included.</param>
/// <param name="SetsWithoutSessionCeiling">
/// What balancing would have chosen with the per-session ceiling lifted
/// (<see cref="WeeklySetAllocation.AllocateWithoutSessionCeiling"/>).
/// </param>
/// <param name="PerSessionWithoutSessionCeiling">Planned volume per session and muscle in that allocation.</param>
/// <param name="Targets">The week's target and MRV per muscle group.</param>
public sealed record BalancedWeek(
    IReadOnlyDictionary<Guid, decimal> Stimulative,
    IReadOnlyDictionary<Guid, decimal> Raw,
    IReadOnlyDictionary<Guid, int> SetsWithoutSessionCeiling,
    IReadOnlyDictionary<(Guid SessionId, Guid MuscleGroupId), decimal> PerSessionWithoutSessionCeiling,
    IReadOnlyDictionary<Guid, MuscleVolumeTarget> Targets);

/// <summary>
/// Explains why balancing moved one exercise, for the list the lifter sees after a workout.
///
/// <b>The session ceiling</b> is asked causally: did it lower this exercise? It did exactly
/// when the same balancing without the ceiling keeps the exercise higher. A counterfactual
/// of this one exercise alone cannot answer that - when balancing swaps two exercises of the
/// same muscle inside one session, putting either back pushes the session over the ceiling,
/// although the ceiling never asked for the swap (a biceps target did). The muscle named is
/// the one that session would have carried past the ceiling.
///
/// <b>The week</b> is read from the final allocation with this one exercise put back where
/// the lifter last saw it - "what would this muscle look like had the proposal not moved".
/// The starting state cannot answer that: the pressure that moved an exercise often does not
/// exist before balancing (a pull-up raised for back drags biceps over its target, and only
/// then does a curl have to come down). Both weekly limits count: short of or past the
/// target, and past MRV, which is measured in raw sets because every set spends recovery.
///
/// The direction is the one the lifter sees - from the previous proposal to the new one -
/// not the distance from the prescription. A proposal can come down from 6 to 5 and still
/// sit above a prescription of 4; reading it against the prescription explained that cut as
/// a raise.
/// </summary>
public static class SetChangeExplanation
{
    /// <summary>
    /// The cause of one exercise moving from <paramref name="previousSets"/> to
    /// <paramref name="allocatedSets"/>, or null when no limit the system has explains it.
    /// </summary>
    public static SetChangeCause? Explain(
        ExerciseSetSlot slot,
        int previousSets,
        int allocatedSets,
        BalancedWeek week)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(week);

        var direction = allocatedSets - previousSets;
        if (direction == 0)
        {
            return null;
        }

        if (direction < 0
            && week.SetsWithoutSessionCeiling.TryGetValue(slot.Id, out var withoutCeiling)
            && allocatedSets < withoutCeiling
            && CeilingMuscle(slot, week) is { } crowded)
        {
            return new SetChangeCause(crowded, SetChangeReason.SessionCeiling);
        }

        Guid? driver = null;
        var widestGap = 0m;

        foreach (var muscle in slot.Muscles)
        {
            if (!week.Targets.TryGetValue(muscle.MuscleGroupId, out var target))
            {
                continue;
            }

            // Ono što je baš ovo pomeranje donelo, vraćeno nazad.
            var undone = muscle.Contribution * (previousSets - allocatedSets);
            var stimulativeWithoutTheMove = week.Stimulative.GetValueOrDefault(muscle.MuscleGroupId) + undone;

            var gap = direction > 0
                ? target.TargetSets - stimulativeWithoutTheMove
                : Math.Max(
                    stimulativeWithoutTheMove - target.TargetSets,
                    week.Raw.GetValueOrDefault(muscle.MuscleGroupId) + undone - target.CeilingSets);

            if (gap > widestGap)
            {
                widestGap = gap;
                driver = muscle.MuscleGroupId;
            }
        }

        return driver is null ? null : new SetChangeCause(driver.Value, SetChangeReason.WeeklyTarget);
    }

    /// <summary>
    /// The muscle this exercise's session would have carried furthest past the ceiling had
    /// the ceiling not existed.
    /// </summary>
    private static Guid? CeilingMuscle(ExerciseSetSlot slot, BalancedWeek week)
    {
        Guid? crowded = null;
        var widestExcess = 0m;

        foreach (var muscle in slot.Muscles)
        {
            if (!week.Targets.ContainsKey(muscle.MuscleGroupId))
            {
                continue;
            }

            var excess = week.PerSessionWithoutSessionCeiling.GetValueOrDefault((slot.SessionId, muscle.MuscleGroupId))
                         - TrainingConstants.MaxSetsPerMusclePerSession;

            if (excess > widestExcess)
            {
                widestExcess = excess;
                crowded = muscle.MuscleGroupId;
            }
        }

        return crowded;
    }
}
