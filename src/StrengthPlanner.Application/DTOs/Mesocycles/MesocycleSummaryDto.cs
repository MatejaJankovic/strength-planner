using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class MesocycleSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Goal Goal { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationWeeks { get; set; }
    public bool IsActive { get; set; }
}
