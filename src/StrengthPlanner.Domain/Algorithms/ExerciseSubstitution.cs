using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// What an exercise needs to know about itself to decide whether it can stand in for another.
/// </summary>
/// <param name="ExerciseId">The exercise.</param>
/// <param name="Type">Compound or isolation.</param>
/// <param name="SuitsLowReps">Whether a compound takes the strength range.</param>
/// <param name="PrimaryMuscleGroupIds">
/// The muscles with a full share. A system exercise has exactly one; a lifter's own exercise may
/// have several.
/// </param>
/// <param name="IsLoadable">
/// Whether the exercise carries a load the plan can prescribe: false for a hold such as the
/// plank, which carries bodyweight equipment with no bodyweight share.
/// </param>
public sealed record SubstitutionProfile(
    Guid ExerciseId,
    ExerciseType Type,
    bool SuitsLowReps,
    IReadOnlyCollection<Guid> PrimaryMuscleGroupIds,
    bool IsLoadable);

/// <summary>
/// Which exercise may replace another for the rest of a block.
///
/// A swap changes the exercise and nothing else in the plan: the rep range, the target RIR and
/// the set count stay what the block prescribed for that slot. That is only right if the new
/// exercise would have been prescribed the same, and the prescription of a built-in block is
/// decided by exactly three things (<see cref="GoalPrescriptions"/>, round 14): whether it is a
/// compound, whether a compound suits the strength range, and the muscle the slot is there to
/// train. So the replacement must match all three:
/// <list type="bullet">
/// <item>the same primary muscles, or the week's volume would move to another muscle and the
/// slot would stop doing what the template put it there for. Compared as a set, so a lifter's
/// own exercise with two primaries matches only one that has the same two;</item>
/// <item>the same type, or an isolation would take a compound's place in the session's
/// compound budget (<see cref="SessionComposition"/>);</item>
/// <item>for a compound, the same answer to <c>SuitsLowReps</c>, or a split squat would inherit
/// a back squat's 3-6 in a strength block - the case round 14 removed.</item>
/// </list>
/// A hold without load is never offered: the plan could not prescribe or progress it.
/// </summary>
public static class ExerciseSubstitution
{
    /// <summary>Whether <paramref name="candidate"/> can replace <paramref name="current"/>.</summary>
    public static bool CanReplace(SubstitutionProfile current, SubstitutionProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.ExerciseId != current.ExerciseId
               && candidate.IsLoadable
               && current.PrimaryMuscleGroupIds.Count > 0
               && current.PrimaryMuscleGroupIds.ToHashSet().SetEquals(candidate.PrimaryMuscleGroupIds)
               && candidate.Type == current.Type
               && (current.Type == ExerciseType.Isolation || candidate.SuitsLowReps == current.SuitsLowReps);
    }
}
