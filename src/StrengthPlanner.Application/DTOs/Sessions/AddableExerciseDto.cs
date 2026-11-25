namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>Vežba koja sme da se doda u dan bloka koji već teče.</summary>
public class AddableExerciseDto
{
    public Guid ExerciseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public bool IsCustom { get; set; }

    /// <summary>Mišići koje vežba direktno radi (puni udeo), da bi se birala po mišiću, ne samo po imenu.</summary>
    public IReadOnlyList<string> PrimaryMuscles { get; set; } = [];
}
