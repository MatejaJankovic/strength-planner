using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.Exercises;

/// <summary>
/// Zahtev za kreiranje korisničke (custom) vežbe.
/// </summary>
public class CreateExerciseRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Compound" ili "Isolation" (string zbog čitljivosti API ugovora).</summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Equipment { get; set; } = string.Empty;

    /// <summary>Bar jedna mišićna grupa; Contribution 1.0 (primarna) ili 0.5 (sekundarna).</summary>
    [Required]
    [MinLength(1)]
    public List<MuscleContributionDto> Muscles { get; set; } = new();
}
