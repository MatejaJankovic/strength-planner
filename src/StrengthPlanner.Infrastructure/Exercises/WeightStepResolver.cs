using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Exercises;

/// <summary>
/// Rešava efektivni korak opterećenja po vežbi: korisnički override ima prednost
/// nad podrazumevanim korakom vežbe. Koriste ga generator mezociklusa i progresija
/// da bi obe strane računale sa istim korakom.
/// </summary>
internal static class WeightStepResolver
{
    public static async Task<Dictionary<Guid, decimal>> ResolveAsync(
        AppDbContext db,
        Guid userId,
        IReadOnlyCollection<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        // Vidljivost vežbi je i ovde uslovljena korisnikom, iako pozivaoci već filtriraju:
        // helper ne sme da zavisi od discipline pozivaoca.
        var defaults = await db.Exercises
            .AsNoTracking()
            .Where(exercise => exerciseIds.Contains(exercise.Id)
                               && (!exercise.IsCustom || exercise.CreatedByUserId == userId))
            .Select(exercise => new { exercise.Id, exercise.WeightStepKg })
            .ToDictionaryAsync(exercise => exercise.Id, exercise => exercise.WeightStepKg, cancellationToken);

        var overrides = await db.UserExerciseSettings
            .AsNoTracking()
            .Where(setting => setting.UserId == userId && exerciseIds.Contains(setting.ExerciseId))
            .ToDictionaryAsync(
                setting => setting.ExerciseId,
                setting => setting.WeightStepKg,
                cancellationToken);

        foreach (var (exerciseId, stepKg) in overrides)
        {
            defaults[exerciseId] = stepKg;
        }

        return defaults;
    }

    /// <summary>Korak za vežbu, uz globalni default kada vežba nije u mapi.</summary>
    public static decimal StepFor(IReadOnlyDictionary<Guid, decimal> stepsByExerciseId, Guid exerciseId)
    {
        return stepsByExerciseId.TryGetValue(exerciseId, out var stepKg)
            ? stepKg
            : TrainingConstants.WeightStepKg;
    }

    /// <summary>
    /// Svi korisnički override-i, za mapiranja koja podrazumevani korak već imaju
    /// učitan kroz Exercise navigaciju.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> LoadOverridesAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.UserExerciseSettings
            .AsNoTracking()
            .Where(setting => setting.UserId == userId)
            .ToDictionaryAsync(
                setting => setting.ExerciseId,
                setting => setting.WeightStepKg,
                cancellationToken);
    }

    /// <summary>Override ako postoji, inače podrazumevani korak same vežbe.</summary>
    public static decimal Effective(
        IReadOnlyDictionary<Guid, decimal> overrides,
        Guid exerciseId,
        decimal defaultStepKg)
    {
        return overrides.TryGetValue(exerciseId, out var stepKg) ? stepKg : defaultStepKg;
    }
}
