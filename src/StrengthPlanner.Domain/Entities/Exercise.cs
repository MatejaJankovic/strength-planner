using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Vežba — sistemska (IsCustom = false, CreatedByUserId = null) ili korisnička.
/// </summary>
public class Exercise
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public ExerciseType Type { get; set; }
    public string Equipment { get; set; } = null!;
    public bool IsCustom { get; set; }
    public Guid? CreatedByUserId { get; set; }

    // Podrazumevani korak opterećenja za ovu vežbu (izveden iz sprave pri seed-u).
    // Korisnik ga može pregaziti kroz UserExerciseSetting.
    public decimal WeightStepKg { get; set; } = TrainingConstants.WeightStepKg;

    // Frakcioni doprinos mišićnim grupama (primarna 1.0, sekundarna 0.5).
    public ICollection<ExerciseMuscle> Muscles { get; set; } = new List<ExerciseMuscle>();

    // Obrnute veze.
    public ICollection<ExercisePlan> ExercisePlans { get; set; } = new List<ExercisePlan>();
    public ICollection<OneRepMaxRecord> OneRepMaxRecords { get; set; } = new List<OneRepMaxRecord>();
}
