using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Notes;

/// <summary>Reads the lifter's notes about a set of exercises, for the screens that show them.</summary>
public static class ExerciseNoteResolver
{
    /// <summary>Exercise id to note text; an exercise without a note is simply absent.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        AppDbContext db,
        Guid userId,
        IReadOnlyCollection<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.UserExerciseNotes
            .AsNoTracking()
            .Where(note => note.UserId == userId && exerciseIds.Contains(note.ExerciseId))
            .ToDictionaryAsync(note => note.ExerciseId, note => note.Text, cancellationToken);
    }
}
