namespace StrengthPlanner.Application.DTOs.Analytics;

/// <summary>
/// Ukupna tonaža (zbir težina × ponavljanja svih logovanih serija) jedne nedelje mezociklusa.
/// </summary>
public class WeeklyTonnageDto
{
    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }
    public decimal TonnageKg { get; set; }
}
