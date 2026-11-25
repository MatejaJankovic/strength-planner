using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Notes;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Notes;

public class WorkoutNoteService : IWorkoutNoteService
{
    private readonly AppDbContext _db;

    public WorkoutNoteService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<NoteDto> SetSessionNoteAsync(
        Guid userId,
        Guid sessionId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var text = Validate(note);

        // Direct UPDATE of the one column: the note is not training data, so it must not go through
        // the lifecycle (no status check, no revision flag) and must not rewrite anything else.
        var updated = await _db.WorkoutSessions
            .Where(session => session.Id == sessionId
                              && session.TrainingWeek.Mesocycle.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.Note, text),
                cancellationToken);

        if (updated == 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        return new NoteDto { Note = text };
    }

    public async Task<NoteDto> SetExerciseNoteAsync(
        Guid userId,
        Guid exerciseId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var text = Validate(note);

        // The ownership filter on Exercise hides another lifter's custom exercise, so it is a 404
        // here rather than a note attached to something the lifter cannot see.
        if (!await _db.Exercises.AnyAsync(exercise => exercise.Id == exerciseId, cancellationToken))
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise was not found.");
        }

        var existing = await _db.UserExerciseNotes.FirstOrDefaultAsync(
            userNote => userNote.UserId == userId && userNote.ExerciseId == exerciseId,
            cancellationToken);

        if (text is null)
        {
            if (existing is not null)
            {
                _db.UserExerciseNotes.Remove(existing);
            }
        }
        else if (existing is null)
        {
            _db.UserExerciseNotes.Add(new UserExerciseNote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                Text = text,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Text = text;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new NoteDto { Note = text };
    }

    /// <summary>The trimmed note, or null for an empty one; too long is a 400 even if the DTO check was skipped.</summary>
    private static string? Validate(string? note)
    {
        var text = NotePolicy.Normalize(note);

        if (text is not null && text.Length > NotePolicy.MaximumLength)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                $"A note can be at most {NotePolicy.MaximumLength} characters.");
        }

        return text;
    }
}
