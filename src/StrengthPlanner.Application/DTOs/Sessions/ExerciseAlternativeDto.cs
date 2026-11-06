namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>Vežba koja sme da zameni planiranu za ostatak bloka.</summary>
public class ExerciseAlternativeDto
{
    public Guid ExerciseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}
