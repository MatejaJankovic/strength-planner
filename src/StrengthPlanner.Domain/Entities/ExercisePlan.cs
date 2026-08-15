namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Planirana vežba u treningu: rep-opseg, ciljni broj serija i RIR, te
/// preporučeno opterećenje koje popunjava algoritam (double progression + auto-regulacija).
/// </summary>
public class ExercisePlan
{
    public Guid Id { get; set; }

    public Guid WorkoutSessionId { get; set; }
    public WorkoutSession WorkoutSession { get; set; } = null!;

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int Order { get; set; }

    // Predlog koji korisnik vidi: propis nedelje pomeren tako da nedelja padne u ciljnu
    // zonu volumena za mišiće koje vežba pogađa.
    public int TargetSets { get; set; }

    // Šta nivo iskustva i periodizacija propisuju za ovu nedelju, pre balansiranja
    // volumena. Sidro oko koga se predlog pomera i mera koliko je pomeren — zato se
    // upisuje samo kada se propis stvarno menja (generisanje bloka, deload).
    public int PrescribedSets { get; set; }

    public int RepRangeMin { get; set; }
    public int RepRangeMax { get; set; }
    public int TargetRir { get; set; } // ciljni RIR; RPE = 10 - RIR
    public decimal? TargetWeightKg { get; set; } // preporučeno opterećenje; puni ga algoritam

    public ICollection<SetLog> SetLogs { get; set; } = new List<SetLog>();
}
