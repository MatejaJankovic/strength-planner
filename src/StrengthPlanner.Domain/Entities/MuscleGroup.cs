namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Mišićna grupa (npr. "Chest"). Nosilac MEV/MRV granica i frakcionog volumena.
/// </summary>
public class MuscleGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    // Obrnute veze.
    public ICollection<ExerciseMuscle> ExerciseMuscles { get; set; } = new List<ExerciseMuscle>();
    public VolumeLandmark? VolumeLandmark { get; set; }
}
