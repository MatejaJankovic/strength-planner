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
    public int TargetSets { get; set; }
    public int RepRangeMin { get; set; }
    public int RepRangeMax { get; set; }
    public int TargetRir { get; set; } // ciljni RIR; RPE = 10 - RIR
    public decimal? TargetWeightKg { get; set; } // preporučeno opterećenje; puni ga algoritam

    public ICollection<SetLog> SetLogs { get; set; } = new List<SetLog>();
}
