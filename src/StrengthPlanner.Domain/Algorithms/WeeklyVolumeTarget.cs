using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Where one muscle group's weekly volume should land in <b>this</b> week of the block.
///
/// Set balancing aims every week at MAV, which is the right target for a flat block and
/// the wrong one for every other week of a periodized block. Measured on a linear
/// hypertrophy block: the prescription for chest ran 20, 20, 16, 16, 12 sets across the
/// five training weeks, and after balancing every week came out at 16. The volume phase
/// and the intensity phase existed in the prescription and were erased in the proposal -
/// so the periodization model changed reps and reserve, and the one thing it says out
/// loud, the amount of work, it did not change at all.
///
/// Two corrections, and both are multiplications of the same number:
///
/// <list type="bullet">
/// <item><b>The week.</b> The target moves exactly as far as the prescription moved it -
/// the ratio of this week's prescribed volume to the base week's. A flat block has a ratio
/// of one, so its target stays MAV and nothing about existing blocks changes.</item>
/// <item><b>The goal.</b> Volume landmarks are hypertrophy landmarks; MAV is defined as
/// the volume that drives growth. A strength block does fewer, heavier sets and pays more
/// recovery for each, so it aims at the middle of the lifter's own MEV-MAV band rather
/// than at its top. That keeps the number learned from their data instead of scaled by a
/// constant.</item>
/// </list>
///
/// The result is bounded by the landmarks themselves: never below MEV, because a planned
/// week should at least maintain, and never above MRV, because that is where recovery
/// stops.
/// </summary>
public static class WeeklyVolumeTarget
{
    /// <summary>
    /// Weekly volume a block of this goal aims at, before the week's own shift.
    /// </summary>
    public static decimal ForGoal(Goal goal, VolumeLandmarkValues landmarks)
    {
        ArgumentNullException.ThrowIfNull(landmarks);

        return goal == Goal.Strength
            ? (landmarks.Mev + landmarks.Mav) / 2m
            : landmarks.Mav;
    }

    /// <summary>
    /// Weekly volume this week aims at.
    /// </summary>
    /// <param name="goal">Goal of the block the week belongs to.</param>
    /// <param name="landmarks">The lifter's own MEV/MAV/MRV for this muscle group.</param>
    /// <param name="prescribedSets">
    /// Volume this week prescribes for the muscle group: every exercise's prescribed sets
    /// weighted by how much of a set it contributes.
    /// </param>
    /// <param name="baseSets">
    /// The same sum for the block's base week, recovered by inverting the week's set shift.
    /// Zero or less means the shift is unknown, and the week then aims at the goal target
    /// unchanged rather than at an invented one.
    /// </param>
    public static decimal ForWeek(
        Goal goal,
        VolumeLandmarkValues landmarks,
        decimal prescribedSets,
        decimal baseSets)
    {
        ArgumentNullException.ThrowIfNull(landmarks);

        var target = ForGoal(goal, landmarks);

        if (baseSets > 0)
        {
            target *= prescribedSets / baseSets;
        }

        return Math.Clamp(target, landmarks.Mev, landmarks.Mrv);
    }
}
