namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Lične MEV/MRV granice po mišićnoj grupi. Seed vrednosti iz <see cref="VolumeLandmark"/>
/// su populacioni prosek; ovaj red pamti koliko volumena konkretan korisnik stvarno
/// podnosi i pomera se najviše za jednu seriju po završenoj nedelji.
/// </summary>
public class UserVolumeLandmark
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public Guid MuscleGroupId { get; set; }
    public MuscleGroup MuscleGroup { get; set; } = null!;

    public int Mev { get; set; }
    public int Mrv { get; set; }

    /// <summary>Kraj poslednje nedelje koja je pomerila granice — sprečava dvostruko računanje.</summary>
    public DateTime UpdatedAt { get; set; }
}
