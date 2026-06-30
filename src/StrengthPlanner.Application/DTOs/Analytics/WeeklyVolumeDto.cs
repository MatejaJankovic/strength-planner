namespace StrengthPlanner.Application.DTOs.Analytics;

public class WeeklyVolumeDto
{
    public string Muscle { get; set; } = string.Empty;
    public decimal Sets { get; set; }
    public int Mev { get; set; }
    public int Mrv { get; set; }
    public string Status { get; set; } = string.Empty;
}
