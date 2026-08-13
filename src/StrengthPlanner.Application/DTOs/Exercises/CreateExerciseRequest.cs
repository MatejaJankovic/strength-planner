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

    // Kolona je varchar(32); duži tekst je ranije prolazio validaciju i padao kao 500.
    [Required]
    [MaxLength(32)]
    public string Equipment { get; set; } = string.Empty;

    /// <summary>
    /// Bar jedna mišićna grupa; Contribution 1.0 (primarna) ili 0.5 (sekundarna).
    /// Gornja granica prati broj mišićnih grupa koje sistem uopšte poznaje — bez nje se
    /// proizvoljno velika lista obrađivala u celosti pre nego što bi upit pao.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(MaxMuscleGroups)]
    public List<MuscleContributionDto> Muscles { get; set; } = new();

    /// <summary>
    /// Koliko mišićnih grupa sistem uopšte poznaje.
    ///
    /// Mora da ostane <c>const</c> jer ga traži atribut validacije, pa ne može da se izvede
    /// iz <see cref="ExerciseCatalog.MuscleGroupNames"/>. Da razmimoilaženje ne bi tiho
    /// počelo da odbija ispravne zahteve, poklapanje čuva test.
    /// </summary>
    public const int MaxMuscleGroups = 10;
}
