using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>Base rep range and target RIR a goal prescribes, before periodization shifts it.</summary>
public sealed record GoalPrescription(int RepRangeMin, int RepRangeMax, int TargetRir);

/// <summary>
/// Translates a training goal into the rep range and proximity to failure it asks for.
///
/// Strength lives in low reps against heavy load; hypertrophy in the middle range with a
/// rep or two left in reserve. Both numbers are training rules, not configuration, which is
/// why they live in the domain: the generator writes them into the first block, and the
/// deload logic needs the same values later to work out what a week should look like once
/// fatigue has rearranged the block.
/// </summary>
public static class GoalPrescriptions
{
    public static GoalPrescription ForGoal(Goal goal) => goal switch
    {
        Goal.Strength => new GoalPrescription(RepRangeMin: 3, RepRangeMax: 6, TargetRir: 2),
        Goal.Hypertrophy => new GoalPrescription(RepRangeMin: 8, RepRangeMax: 12, TargetRir: 1),
        _ => throw new ArgumentOutOfRangeException(nameof(goal), goal, "Unsupported training goal.")
    };

    /// <summary>
    /// The same prescription, narrowed to what one exercise should actually carry.
    ///
    /// A block goal describes how the lifter intends to get stronger, and strength is
    /// expressed in the movements that can carry load. An isolation exercise cannot: a set
    /// of three lateral raises is not a test of force production, it is a way to load a
    /// shoulder joint with a weight the muscle was never the limiting factor for. The
    /// handbook puts compounds and isolations on opposite sides of exactly this line, and
    /// an advanced lifter's strength session is one compound and five isolations - all of
    /// which used to be prescribed at 3-6 reps, and at 3-4 in the intensity week.
    ///
    /// So the rep range follows the exercise and the target RIR follows the block. Reserve
    /// is how hard the week is meant to be, and that is a property of the week, not of the
    /// movement; keeping it from the goal is also what lets the deload restore a plan from
    /// one number per block. For a hypertrophy block nothing changes at all - isolation
    /// work was already prescribed in its range.
    /// </summary>
    public static GoalPrescription ForExercise(Goal goal, ExerciseType type)
    {
        var goalPrescription = ForGoal(goal);

        if (type != ExerciseType.Isolation)
        {
            return goalPrescription;
        }

        var accessory = ForGoal(Goal.Hypertrophy);

        return goalPrescription with
        {
            RepRangeMin = accessory.RepRangeMin,
            RepRangeMax = accessory.RepRangeMax
        };
    }
}
