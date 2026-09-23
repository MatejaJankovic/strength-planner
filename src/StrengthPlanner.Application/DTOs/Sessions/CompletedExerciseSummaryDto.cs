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
}
