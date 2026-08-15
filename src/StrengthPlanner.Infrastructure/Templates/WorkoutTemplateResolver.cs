using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Templates;

/// <inheritdoc />
public class WorkoutTemplateResolver : IWorkoutTemplateResolver
{
    private readonly AppDbContext _db;

    public WorkoutTemplateResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ResolvedTemplate?> ResolveAsync(
        Guid userId,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        if (CustomTemplateKey.TryParse(templateKey, out var templateId))
        {
            return await ResolveCustomAsync(userId, templateId, cancellationToken);
        }

        return await ResolveBuiltInAsync(userId, templateKey, cancellationToken);
    }

    public async Task<string?> NameForAsync(
        Guid userId,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        if (!CustomTemplateKey.TryParse(templateKey, out var templateId))
        {
            return WorkoutTemplateCatalog.GetByKey(templateKey)?.Name;
        }

        return await _db.UserWorkoutTemplates
            .AsNoTracking()
            .Where(template => template.Id == templateId && template.UserId == userId)
            .Select(template => template.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lični šablon prolazi neskraćen: korisnik je sam izabrao vežbe, pa mu se ne uklanjaju.
    /// </summary>
    private async Task<ResolvedTemplate?> ResolveCustomAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await _db.UserWorkoutTemplates
            .AsNoTracking()
            .Include(template => template.Days)
                .ThenInclude(day => day.Exercises)
                    .ThenInclude(exercise => exercise.Exercise)
            .FirstOrDefaultAsync(
                template => template.Id == templateId && template.UserId == userId,
                cancellationToken);

        if (template is null)
        {
            return null;
        }

        var days = template.Days
            .OrderBy(day => day.Order)
            .Select(day => new ResolvedTemplateDay(
                day.Name,
                day.Exercises
                    .OrderBy(exercise => exercise.Order)
                    .Select(exercise => new ResolvedTemplateExercise(
                        exercise.Exercise,
                        exercise.Sets,
                        exercise.RepRangeMin,
                        exercise.RepRangeMax))
                    .ToList()))
            .ToList();

        return new ResolvedTemplate(
            CustomTemplateKey.For(template.Id),
            template.Name,
            IsCustom: true,
            days);
    }

    /// <summary>
    /// Ugrađeni šablon je ponuda: nosi više vežbi nego što trening dobija, a koliko ih i
    /// kojih ulazi bira nivo iskustva. Skraćivanje se dešava ovde, da bi generator dobio
    /// isti oblik kao za lični šablon.
    /// </summary>
    private async Task<ResolvedTemplate?> ResolveBuiltInAsync(
        Guid userId,
        string templateKey,
        CancellationToken cancellationToken)
    {
        var template = WorkoutTemplateCatalog.GetByKey(templateKey);
        if (template is null)
        {
            return null;
        }

        var experienceLevel = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;

        var exerciseNames = template.Days
            .SelectMany(day => day.Exercises)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exercises = await _db.Exercises
            .AsNoTracking()
            .Where(exercise => exerciseNames.Contains(exercise.Name)
                               && (!exercise.IsCustom || exercise.CreatedByUserId == userId))
            .ToListAsync(cancellationToken);

        // Korisnik sme da ima svoju vežbu istog naziva kao sistemska (provera pri pravljenju
        // ne gleda velika i mala slova na isti način kao ovaj upit), pa se po nazivu mogu
        // vratiti dva reda. Šablon uvek misli na sistemsku vežbu - grupisanje sprečava
        // izuzetak zbog dvostrukog ključa i bira pravu.
        var exerciseByName = exercises
            .GroupBy(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(exercise => exercise.IsCustom).First(),
                StringComparer.OrdinalIgnoreCase);

        var missing = exerciseNames
            .Where(exerciseName => !exerciseByName.ContainsKey(exerciseName))
            .ToList();

        if (missing.Count > 0)
        {
            throw new MissingTemplateExercisesException(missing);
        }

        var days = template.Days
            .Select(day => new ResolvedTemplateDay(
                day.Name,
                SessionComposition
                    .ForLevel(
                        day.Exercises.Select(name => exerciseByName[name]).ToList(),
                        exercise => exercise.Type == ExerciseType.Compound,
                        experienceLevel)
                    .Select(exercise => new ResolvedTemplateExercise(exercise, null, null, null))
                    .ToList()))
            .ToList();

        return new ResolvedTemplate(template.Key, template.Name, IsCustom: false, days);
    }
}

/// <summary>
/// Ugrađeni šablon pominje vežbu koje nema u katalogu baze. To nije korisnikova greška nego
/// razlika između koda i seed-a, pa se prijavljuje posebno da poruka ne bi izgledala kao
/// „pogrešan unos".
/// </summary>
public sealed class MissingTemplateExercisesException : Exception
{
    public MissingTemplateExercisesException(IReadOnlyCollection<string> exerciseNames)
        : base($"Template references exercises missing from seed: {string.Join(", ", exerciseNames)}.")
    {
        ExerciseNames = exerciseNames;
    }

    public IReadOnlyCollection<string> ExerciseNames { get; }
}
