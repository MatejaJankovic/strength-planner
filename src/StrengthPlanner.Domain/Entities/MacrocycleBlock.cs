using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Jedan blok dugoročnog plana. Blok postoji kao *namera* od trenutka pravljenja plana,
/// a mezociklus se generiše tek kada blok dođe na red — da bi krenuo od 1RM vrednosti
/// koje važe tada, a ne od procena starih nekoliko meseci.
/// </summary>
public class MacrocycleBlock
{
    public Guid Id { get; set; }

    public Guid MacrocycleId { get; set; }
    public Macrocycle Macrocycle { get; set; } = null!;

    /// <summary>Redosled u planu, počev od 1.</summary>
    public int Order { get; set; }

    public Goal Goal { get; set; }
    public string TemplateKey { get; set; } = null!;

    /// <summary>
    /// Model periodizacije za ovaj blok. Bira se po bloku, jer dugoročan plan i dobija
    /// smisao time što se raspored menja: blok volumena pa blok intenziteta.
    /// </summary>
    public PeriodizationModel PeriodizationModel { get; set; } = PeriodizationModel.Flat;

    /// <summary>
    /// Ko odlučuje o broju serija u ovom bloku. Bira se po bloku, kao i model
    /// periodizacije: isti plan sme da ima blok koji prati lični šablon doslovno i blok
    /// koji cilja volumen.
    /// </summary>
    public SetAllocation SetAllocation { get; set; } = SetAllocation.TargetVolume;

    /// <summary>Mezociklus generisan za ovaj blok; null dok blok nije došao na red.</summary>
    public Guid? MesocycleId { get; set; }
    public Mesocycle? Mesocycle { get; set; }

    /// <summary>
    /// Kada je generisanje preuzeto. Upisuje se uslovnim UPDATE-om pre samog
    /// generisanja, pa dva istovremena završetka poslednjeg treninga ne mogu da
    /// naprave dva mezociklusa za isti blok.
    /// </summary>
    public DateTime? GeneratedAt { get; set; }
}
