using StrengthPlanner.Application.DTOs.Sessions;

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

    /// <summary>The lifter's note about this workout; null when there is none.</summary>
    public string? Note { get; set; }

    /// <summary>Set only for a finished workout: whether it can still be corrected.</summary>
    public SessionCorrectionDto? Correction { get; set; }
    public List<ExercisePlanDto> ExercisePlans { get; set; } = new();
}
