namespace StrengthPlanner.Application.DTOs.Sessions;

public class CompletedExerciseSummaryDto
{
    public Guid ExercisePlanId { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public decimal? E1Rm { get; set; }
    public bool IsPr { get; set; }
    public decimal? NextWeightKg { get; set; }
    public bool WeightIncreased { get; set; }
}
