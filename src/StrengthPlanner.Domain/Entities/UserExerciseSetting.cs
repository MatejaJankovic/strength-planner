namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Korisnički override podrazumevanih parametara vežbe. Red postoji samo ako je
/// korisnik nešto stvarno promenio; u suprotnom važi vrednost sa same vežbe.
/// </summary>
public class UserExerciseSetting
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    /// <summary>Korak opterećenja koji korisnik koristi za ovu vežbu (kg).</summary>
    public decimal WeightStepKg { get; set; }
}
