namespace StrengthPlanner.Application.DTOs.Analytics;

public class PersonalRecordDto
{
    public Guid ExerciseId { get; set; }
    public string Exercise { get; set; } = string.Empty;
    public decimal? BestE1Rm { get; set; }
    public decimal? BestWeight { get; set; }
    public DateTime? AchievedAt { get; set; }
}
