namespace StrengthPlanner.Application.DTOs.Sessions;

public class CompletedExerciseSummaryDto
{
    public Guid ExercisePlanId { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public decimal? E1Rm { get; set; }
    public bool IsPr { get; set; }

    /// <summary>Heaviest load actually lifted, or the planned one when the exercise was skipped.</summary>
    public decimal? UsedWeightKg { get; set; }

    /// <summary>Load proposed for the next week of this block; null when there is no next week.</summary>
    public decimal? NextWeightKg { get; set; }

    /// <summary>Difference between the proposal and <see cref="UsedWeightKg"/>, or null.</summary>
    public decimal? WeightChangeKg { get; set; }

    /// <summary>True only when the proposal is heavier than what was lifted.</summary>
    public bool WeightIncreased { get; set; }

    /// <summary>
    /// True when this exercise carries a share of the lifter's body, so every load above is
    /// what was <b>added</b> to it. False for a bodyweight exercise whose owner has no
    /// recorded body mass — nothing is known to add, so it reads like any other exercise.
    /// </summary>
    public bool IsBodyweight { get; set; }

    /// <summary>Body mass the movement carries, in kilograms; zero for external load.</summary>
    public decimal BodyweightLoadKg { get; set; }

    /// <summary>
    /// True when the rule wanted less than body mass alone, so the proposal is zero added
    /// kilograms and progress continues through reps.
    /// </summary>
    public bool LoadFloorReached { get; set; }
}
