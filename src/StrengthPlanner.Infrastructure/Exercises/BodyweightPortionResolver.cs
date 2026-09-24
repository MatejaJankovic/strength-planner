using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Exercises;

/// <summary>
/// How many kilograms of the lifter's own body each exercise carries for them right now.
///
/// Sits beside <see cref="WeightStepResolver"/> and answers the same shape of question: a
/// value that depends on both the exercise and the user, which the training rules need but
/// the domain cannot read from the database.
/// </summary>
public static class BodyweightPortionResolver
{
    /// <summary>
    /// Portion per exercise, in kilograms. Exercises loaded externally are absent, which
    /// reads as zero through <see cref="PortionFor"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, decimal>> ResolveAsync(
        AppDbContext db,
        Guid userId,
        IReadOnlyList<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(exerciseIds);

        var shares = await db.Exercises
            .AsNoTracking()
            .Where(exercise => exerciseIds.Contains(exercise.Id) && exercise.BodyweightShare > 0)
            .Select(exercise => new { exercise.Id, exercise.BodyweightShare })
            .ToListAsync(cancellationToken);

        if (shares.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var bodyweightKg = await LoadBodyweightAsync(db, userId, cancellationToken);

        return shares.ToDictionary(
            exercise => exercise.Id,
            exercise => BodyweightLoad.PortionKg(bodyweightKg, exercise.BodyweightShare));
    }

    /// <summary>
    /// The lifter body mass, or zero when the profile has none recorded.
    ///
    /// Zero is not a special case downstream: it makes every portion zero, so a bodyweight
    /// exercise behaves exactly as it did before this model existed and counts only what was
    /// added. Callers that already hold the exercises (with their shares) use this and
    /// <see cref="BodyweightLoad.PortionKg"/> instead of the dictionary above.
    /// </summary>
    public static async Task<decimal> LoadBodyweightAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return await db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (decimal?)profile.BodyweightKg)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }

    /// <summary>Portion for one exercise; zero when it carries no body mass.</summary>
    public static decimal PortionFor(IReadOnlyDictionary<Guid, decimal> portions, Guid exerciseId)
    {
        ArgumentNullException.ThrowIfNull(portions);

        return portions.GetValueOrDefault(exerciseId);
    }
}
