using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Istorija procenjenog (Epley) ili ručno unetog 1RM po vežbi — za trend i
/// startno opterećenje novog mezociklusa.
/// </summary>
public class OneRepMaxRecord
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public decimal ValueKg { get; set; }
    public OneRepMaxSource Source { get; set; }
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// The workout this estimate came from, so correcting that workout can replace it. Null
    /// for typed maxima, test sets and estimates written before they were tied to a workout.
    /// </summary>
    public Guid? WorkoutSessionId { get; set; }
}
