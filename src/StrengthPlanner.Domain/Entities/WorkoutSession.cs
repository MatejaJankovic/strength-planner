using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Jedan trening (npr. "Day A") unutar nedelje.
/// </summary>
public class WorkoutSession
{
    public Guid Id { get; set; }

    public Guid TrainingWeekId { get; set; }
    public TrainingWeek TrainingWeek { get; set; } = null!;

    public string DayLabel { get; set; } = null!;
    public DateTime? Date { get; set; }
    public SessionStatus Status { get; set; }

    public ICollection<ExercisePlan> ExercisePlans { get; set; } = new List<ExercisePlan>();
}
