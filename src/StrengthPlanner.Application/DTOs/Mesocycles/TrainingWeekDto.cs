namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class TrainingWeekDto
{
    public Guid Id { get; set; }
    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }
    public List<WorkoutSessionDto> Sessions { get; set; } = new();
}
