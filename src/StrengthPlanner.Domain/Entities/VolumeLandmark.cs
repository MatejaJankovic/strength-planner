namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Statične MEV/MRV granice po mišićnoj grupi (nedeljne radne serije) — seed podaci.
/// </summary>
public class VolumeLandmark
{
    public Guid Id { get; set; }

    public Guid MuscleGroupId { get; set; }
    public MuscleGroup MuscleGroup { get; set; } = null!;

    public int Mev { get; set; } // minimalni efektivni volumen (nedeljne serije)
    public int Mrv { get; set; } // maksimalni oporavljivi volumen (nedeljne serije)
}
