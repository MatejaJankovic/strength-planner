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
    public int Rir { get; set; } // stvarni RIR koji je korisnik uneo
    public DateTime PerformedAt { get; set; }
}
