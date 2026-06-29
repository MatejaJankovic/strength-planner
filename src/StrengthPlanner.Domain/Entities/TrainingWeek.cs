namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Mikrociklus (nedelja) unutar mezociklusa. Nedelja 4 je planirani deload.
/// </summary>
public class TrainingWeek
{
    public Guid Id { get; set; }

    public Guid MesocycleId { get; set; }
    public Mesocycle Mesocycle { get; set; } = null!;

    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }

    public ICollection<WorkoutSession> Sessions { get; set; } = new List<WorkoutSession>();
}
