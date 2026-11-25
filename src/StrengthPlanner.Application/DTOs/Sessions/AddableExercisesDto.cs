namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>
/// Šta sme da se doda u dan bloka koji teče. Razlog zbog koga dodavanja nema stiže kao podatak u odgovoru,
/// a ne kao poruka greške: server u produkciji ne šalje tekst izuzetka (<c>docs/security.md</c>), pa bi
/// vežbač video samo opšti naslov.
/// </summary>
public class AddableExercisesDto
{
    public IReadOnlyList<AddableExerciseDto> Exercises { get; set; } = [];

    /// <summary>Zašto se u ovaj blok ne dodaju vežbe; null kada se dodaju.</summary>
    public string? Unavailable { get; set; }
}
