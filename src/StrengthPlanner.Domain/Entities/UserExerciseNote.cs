namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// The lifter's own note about an exercise, such as the seat and pin settings of a machine or
/// the cue that keeps a shoulder comfortable.
///
/// It belongs to the lifter and the exercise, not to a workout: it is shown every time that
/// exercise comes up, in this block and in every later one, and it survives an exercise being
/// swapped in or out of a plan. A row exists only while there is something to say; clearing the
/// note deletes it. A note is not training data: nothing in the planning or the load rules reads it.
/// </summary>
public class UserExerciseNote
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju), kao kod UserExerciseSetting.
    public Guid UserId { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
}
