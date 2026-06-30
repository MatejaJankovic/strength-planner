namespace StrengthPlanner.Application.DTOs.OneRepMax;

public class OneRepMaxDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string Exercise { get; set; } = string.Empty;
    public decimal ValueKg { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
