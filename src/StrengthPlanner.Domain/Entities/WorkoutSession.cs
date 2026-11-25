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

    /// <summary>
    /// When the workout was completed. Null for workouts completed before what completing
    /// produces was tied to the workout: their estimate and their week's landmark move cannot
    /// be told from anyone else's, so they are corrected as history and nothing is recalculated.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// True while the sets of a completed workout were corrected and the correction has not
    /// been saved yet, that is, the estimate, the next target and the week's score still
    /// describe the old sets.
    /// </summary>
    public bool RevisionPending { get; set; }

    public ICollection<ExercisePlan> ExercisePlans { get; set; } = new List<ExercisePlan>();
}
