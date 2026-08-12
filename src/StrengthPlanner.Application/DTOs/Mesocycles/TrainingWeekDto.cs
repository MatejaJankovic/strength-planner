namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class TrainingWeekDto
{
    public Guid Id { get; set; }
    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }

    /// <summary>Deload koji je uveden procenom umora, a ne planom.</summary>
    public bool IsAutoDeload { get; set; }

    /// <summary>Ocena umora izračunata iz ove nedelje (0-1); null dok nije završena.</summary>
    public decimal? FatigueScore { get; set; }

    public List<WorkoutSessionDto> Sessions { get; set; } = new();
}
