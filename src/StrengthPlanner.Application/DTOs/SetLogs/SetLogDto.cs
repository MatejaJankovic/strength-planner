namespace StrengthPlanner.Application.DTOs.SetLogs;

public class SetLogDto
{
    public Guid Id { get; set; }
    public Guid ExercisePlanId { get; set; }
    public int SetNumber { get; set; }
    public decimal WeightKg { get; set; }
    public int Reps { get; set; }
    public int Rir { get; set; }
    public DateTime PerformedAt { get; set; }
}
