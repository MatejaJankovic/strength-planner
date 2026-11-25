using StrengthPlanner.Application.DTOs.Notes;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// The lifter's notes: one per workout, and one per exercise that is shown every time the
/// exercise comes up. Neither is training data, so writing one is allowed in every status of a
/// workout and changes nothing else.
/// </summary>
public interface IWorkoutNoteService
{
    /// <summary>Sets or clears the note of a workout; 404 when it is not the lifter's.</summary>
    Task<NoteDto> SetSessionNoteAsync(
        Guid userId,
        Guid sessionId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears the lifter's note about an exercise; 404 for an exercise the lifter cannot
    /// see (another lifter's custom exercise).
    /// </summary>
    Task<NoteDto> SetExerciseNoteAsync(
        Guid userId,
        Guid exerciseId,
        string? note,
        CancellationToken cancellationToken = default);
}
