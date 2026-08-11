namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class WorkoutSessionDto
{
    public Guid Id { get; set; }
    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }

    /// <summary>Deload koji je uveden procenom umora, a ne planom.</summary>
    public bool IsAutoDeload { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ExercisePlanDto> ExercisePlans { get; set; } = new();
}
