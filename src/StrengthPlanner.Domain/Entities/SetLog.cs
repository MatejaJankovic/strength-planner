namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Odrađena RADNA serija (zagrevanje se ne prati). Nosi stvarni RIR koji korisnik unese.
/// </summary>
public class SetLog
{
    public Guid Id { get; set; }

    public Guid ExercisePlanId { get; set; }
    public ExercisePlan ExercisePlan { get; set; } = null!;

    public int SetNumber { get; set; }
    public decimal WeightKg { get; set; }
    public int Reps { get; set; }
    public int Rir { get; set; } // stvarni RIR koji je korisnik uneo; kod otkaza uvek 0
    public DateTime PerformedAt { get; set; }

    // Serija izvučena do otkaza — korisnik nije mogao još jedno ponavljanje.
    // Koliko je ponavljanja promašeno u odnosu na rep-opseg računa progresija.
    public bool IsFailure { get; set; }

    // Deo telesne mase koji je ova serija nosila, snimljen u trenutku upisa (masa iz
    // profila × udeo vežbe). Snima se, a ne računa kasnije, da kilogram promenjen u
    // profilu ne bi menjao istoriju: e1RM, tonaža i umor iz ove serije ostaju onakvi
    // kakvi su bili tog dana. Nula za sve što se opterećuje spolja.
    public decimal BodyweightLoadKg { get; set; }
}
