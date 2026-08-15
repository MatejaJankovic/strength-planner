using StrengthPlanner.Application.DTOs.SetLogs;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class ExercisePlanDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int Order { get; set; }

    /// <summary>Predloženi broj radnih serija — propis pomeren ka ciljnoj zoni volumena.</summary>
    public int TargetSets { get; set; }

    /// <summary>
    /// Broj serija koji propisuju nivo iskustva i periodizacija. Razlika u odnosu na
    /// <see cref="TargetSets"/> je tačno ono što je balansiranje volumena pomerilo.
    /// </summary>
    public int PrescribedSets { get; set; }

    public int RepRangeMin { get; set; }
    public int RepRangeMax { get; set; }
    public int TargetRir { get; set; }
    public decimal? TargetWeightKg { get; set; }

    /// <summary>Korak kojim klijent pomera opterećenje za ovu vežbu (kg).</summary>
    public decimal WeightStepKg { get; set; }
    public List<SetLogDto> SetLogs { get; set; } = new();
}
