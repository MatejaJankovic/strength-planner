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
/// <param name="PerSession">Planned volume per session and muscle in the allocation itself.</param>
public sealed record BalancedWeek(
    IReadOnlyDictionary<Guid, decimal> Stimulative,
    IReadOnlyDictionary<Guid, decimal> Raw,
    IReadOnlyDictionary<Guid, int> SetsWithoutSessionCeiling,
    IReadOnlyDictionary<(Guid SessionId, Guid MuscleGroupId), decimal> PerSessionWithoutSessionCeiling,
    IReadOnlyDictionary<Guid, MuscleVolumeTarget> Targets,
    IReadOnlyDictionary<(Guid SessionId, Guid MuscleGroupId), decimal> PerSession);

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

        // Glavno dizanje bloka snage nedeljni cilj ne pomera. Podignuto je samo ka propisu -
        // tu nema šta da se objašnjava mišićem - a spušteno samo zbog granice oporavka.
        if (slot.IsMainLift && direction > 0)
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

        if (slot.IsMainLift)
        {
            return MainLiftCut(slot, previousSets - allocatedSets, week);
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

            var pastMrv = week.Raw.GetValueOrDefault(muscle.MuscleGroupId) + undone - target.CeilingSets;
            var gap = direction > 0
                ? target.TargetSets - stimulativeWithoutTheMove
                : Math.Max(stimulativeWithoutTheMove - target.TargetSets, pastMrv);

            if (gap > widestGap)
            {
                widestGap = gap;
                driver = muscle.MuscleGroupId;
            }
        }

        return driver is null ? null : new SetChangeCause(driver.Value, SetChangeReason.WeeklyTarget);
    }

    /// <summary>
    /// A main lift is cut only by a recovery ceiling, so only a ceiling explains it: MRV if
    /// putting the sets back breaks it, otherwise the session ceiling on the allocation
    /// itself. The second reading is the fallback for a cut two ceilings demand at once -
    /// without the session ceiling MRV would have cut the same set, so the causal test above
    /// does not fire, and MRV with the set put back is not broken either, because the session
    /// ceiling had already taken it. Measured on three weeks of Push/Pull/Legs x2 before this
    /// fallback: a barbell row 5 -> 4 came back with no muscle at all. For a main lift the
    /// one-exercise reading is safe: nothing moves a main lift for the weekly target, so it
    /// cannot be half of a swap the ceiling did not ask for.
    /// </summary>
    private static SetChangeCause? MainLiftCut(ExerciseSetSlot slot, int cutSets, BalancedWeek week)
    {
        Guid? mrvDriver = null;
        var widestMrvGap = 0m;
        Guid? sessionDriver = null;
        var widestSessionGap = 0m;

        foreach (var muscle in slot.Muscles)
        {
            if (!week.Targets.TryGetValue(muscle.MuscleGroupId, out var target))
            {
                continue;
            }

            var undone = muscle.Contribution * cutSets;

            var pastMrv = week.Raw.GetValueOrDefault(muscle.MuscleGroupId) + undone - target.CeilingSets;
            if (pastMrv > widestMrvGap)
            {
                widestMrvGap = pastMrv;
                mrvDriver = muscle.MuscleGroupId;
            }

            var pastSession = week.PerSession.GetValueOrDefault((slot.SessionId, muscle.MuscleGroupId)) + undone
                              - TrainingConstants.MaxSetsPerMusclePerSession;
            if (pastSession > widestSessionGap)
            {
                widestSessionGap = pastSession;
                sessionDriver = muscle.MuscleGroupId;
            }
        }

        if (mrvDriver is not null)
        {
            return new SetChangeCause(mrvDriver.Value, SetChangeReason.WeeklyTarget);
        }

        return sessionDriver is null ? null : new SetChangeCause(sessionDriver.Value, SetChangeReason.SessionCeiling);
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
