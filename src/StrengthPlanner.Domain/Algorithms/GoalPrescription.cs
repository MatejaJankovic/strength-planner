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
}
