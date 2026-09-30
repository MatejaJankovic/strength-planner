using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>The muscle group, and the limit, that best explain one moved set proposal.</summary>
/// <param name="MuscleGroupId">Muscle group whose volume the move served.</param>
/// <param name="Reason">Whether the week's target or one session's ceiling asked for it.</param>
public sealed record SetChangeCause(Guid MuscleGroupId, SetChangeReason Reason);

/// <summary>
/// Explains why balancing moved one exercise, for the list the lifter sees after a workout.
///
/// The explanation is read from the final allocation with this one exercise put back where
/// the lifter last saw it - the question "what would this muscle look like had the proposal
/// not moved". The starting state cannot answer it: the pressure that moved an exercise often
/// does not exist yet before balancing (a pull-up raised for back drags biceps over its
/// target, and only then does a curl have to come down).
///
/// Two limits can ask for a move, and they are checked in order of what they cost the
/// allocator:
///
/// <list type="bullet">
/// <item>A cut that brings a session back under
/// <see cref="TrainingConstants.MaxSetsPerMusclePerSession"/>. Before the ceiling this case
/// did not exist, and the weekly reading misnamed it: the week was below target, so every
/// weekly gap was negative and a cut came back with no muscle, or with a muscle that sits
/// below its target next to a down arrow.</item>
/// <item>The week's distance from the muscle's target, in the direction of the move.</item>
/// </list>
///
/// The direction is the one the lifter sees - from the previous proposal to the new one -
/// not the distance from the prescription. A proposal can come down from 6 to 5 and still sit
/// above a prescription of 4; reading it against the prescription explained that cut as a
/// raise.
/// </summary>
public static class SetChangeExplanation
{
    /// <summary>
    /// The cause of one exercise moving from <paramref name="previousSets"/> to
    /// <paramref name="allocatedSets"/>, or null when nothing the system has limits for
    /// explains it.
    /// </summary>
    /// <param name="slot">The exercise that moved.</param>
    /// <param name="previousSets">What the lifter last saw for it.</param>
    /// <param name="allocatedSets">What balancing chose now.</param>
    /// <param name="finalWeekly">Weekly stimulative volume per muscle after balancing.</param>
    /// <param name="finalPerSession">Planned volume per session and muscle after balancing.</param>
    /// <param name="targetByMuscleGroupId">The week's targets and ceilings.</param>
    public static SetChangeCause? Explain(
        ExerciseSetSlot slot,
        int previousSets,
        int allocatedSets,
        IReadOnlyDictionary<Guid, decimal> finalWeekly,
        IReadOnlyDictionary<(Guid SessionId, Guid MuscleGroupId), decimal> finalPerSession,
        IReadOnlyDictionary<Guid, MuscleVolumeTarget> targetByMuscleGroupId)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(finalWeekly);
        ArgumentNullException.ThrowIfNull(finalPerSession);
        ArgumentNullException.ThrowIfNull(targetByMuscleGroupId);

        var direction = allocatedSets - previousSets;
        if (direction == 0)
        {
            return null;
        }

        Guid? ceilingDriver = null;
        var widestCeilingGap = 0m;
        Guid? weeklyDriver = null;
        var widestWeeklyGap = 0m;

        foreach (var muscle in slot.Muscles)
        {
            if (!targetByMuscleGroupId.TryGetValue(muscle.MuscleGroupId, out var target))
            {
                continue;
            }

            // Ono što je baš ovo pomeranje donelo, vraćeno nazad.
            var undone = muscle.Contribution * (previousSets - allocatedSets);

            if (direction < 0)
            {
                var sessionWithoutTheMove =
                    finalPerSession.GetValueOrDefault((slot.SessionId, muscle.MuscleGroupId)) + undone;
                var ceilingGap = sessionWithoutTheMove - TrainingConstants.MaxSetsPerMusclePerSession;

                if (ceilingGap > widestCeilingGap)
                {
                    widestCeilingGap = ceilingGap;
                    ceilingDriver = muscle.MuscleGroupId;
                }
            }

            var weeklyWithoutTheMove = finalWeekly.GetValueOrDefault(muscle.MuscleGroupId) + undone;
            var weeklyGap = direction > 0
                ? target.TargetSets - weeklyWithoutTheMove
                : weeklyWithoutTheMove - target.TargetSets;

            if (weeklyGap > widestWeeklyGap)
            {
                widestWeeklyGap = weeklyGap;
                weeklyDriver = muscle.MuscleGroupId;
            }
        }

        // Granica treninga košta koliko i MRV, četiri puta više od promašenog cilja, pa
        // kada je ona tražila rez, ona ga i objašnjava.
        if (ceilingDriver is not null)
        {
            return new SetChangeCause(ceilingDriver.Value, SetChangeReason.SessionCeiling);
        }

        return weeklyDriver is null
            ? null
            : new SetChangeCause(weeklyDriver.Value, SetChangeReason.WeeklyTarget);
    }
}
