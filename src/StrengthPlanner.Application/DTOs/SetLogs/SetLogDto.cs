namespace StrengthPlanner.Application.DTOs.SetLogs;

public class SetLogDto
{
    public Guid Id { get; set; }
    public Guid ExercisePlanId { get; set; }
    public int SetNumber { get; set; }
    public decimal WeightKg { get; set; }
    public int Reps { get; set; }
    public int Rir { get; set; }

    /// <summary>Serija izvučena do otkaza (RIR je tada uvek 0).</summary>
    public bool IsFailure { get; set; }

    /// <summary>
    /// Deo telesne mase koji je ova serija nosila, snimljen u trenutku upisa; 0 za spoljno
    /// opterećenje. <see cref="WeightKg"/> ostaje samo dodato opterećenje.
    /// </summary>
    public decimal BodyweightLoadKg { get; set; }

    public DateTime PerformedAt { get; set; }
}
