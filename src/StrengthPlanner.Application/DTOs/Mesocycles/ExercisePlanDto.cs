using StrengthPlanner.Application.DTOs.SetLogs;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class ExercisePlanDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int Order { get; set; }
    public int TargetSets { get; set; }
    public int RepRangeMin { get; set; }
    public int RepRangeMax { get; set; }
    public int TargetRir { get; set; }
    public decimal? TargetWeightKg { get; set; }

    /// <summary>Korak kojim klijent pomera opterećenje za ovu vežbu (kg).</summary>
    public decimal WeightStepKg { get; set; }
    public List<SetLogDto> SetLogs { get; set; } = new();
}
