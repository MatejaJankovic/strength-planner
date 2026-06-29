namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Veza vežba &lt;-&gt; mišićna grupa sa frakcijom za brojanje volumena
/// (Contribution: 1.0 = primarna, 0.5 = sekundarna).
/// </summary>
public class ExerciseMuscle
{
    public Guid Id { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public Guid MuscleGroupId { get; set; }
    public MuscleGroup MuscleGroup { get; set; } = null!;

    public decimal Contribution { get; set; }
}
