namespace StrengthPlanner.Application.DTOs.Exercises;

/// <summary>
/// Doprinos vežbe jednoj mišićnoj grupi (1.0 primarna, 0.5 sekundarna).
/// </summary>
public class MuscleContributionDto
{
    public string MuscleGroup { get; set; } = string.Empty;
    public decimal Contribution { get; set; }
}
