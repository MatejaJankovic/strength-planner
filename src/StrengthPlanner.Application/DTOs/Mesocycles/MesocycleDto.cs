using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class MesocycleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Goal Goal { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationWeeks { get; set; }

    /// <summary>Model periodizacije bloka.</summary>
    public PeriodizationModel PeriodizationModel { get; set; }
    public bool IsActive { get; set; }
    public List<TrainingWeekDto> Weeks { get; set; } = new();
}
