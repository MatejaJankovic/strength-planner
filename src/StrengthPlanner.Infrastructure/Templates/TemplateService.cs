using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Templates;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Templates;

/// <summary>
/// Priprema katalog šablona za prikaz.
///
/// Čarobnjak je do sada nudio pun spisak vežbi iz šablona, a generator je taj spisak
/// skraćivao po nivou iskustva — naprednom vežbaču je obećavao šest vežbi, a davao tri.
/// Ovde se ista funkcija (<see cref="SessionComposition.ForLevel{T}"/>) primenjuje na
/// prikaz, pa je ono što korisnik bira ono što i dobija.
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly AppDbContext _db;

    public TemplateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkoutTemplateDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new { profile.ExperienceLevel, profile.TrainingDaysPerWeek })
            .FirstOrDefaultAsync(cancellationToken);

        var experienceLevel = profile?.ExperienceLevel ?? ExperienceLevel.Intermediate;
        var suggestedKey = profile is null
            ? null
            : WorkoutTemplateCatalog.SuggestedFor(profile.TrainingDaysPerWeek).Key;

        return WorkoutTemplateCatalog
            .GetAll()
            .Select(template => new WorkoutTemplateDto
            {
                Key = template.Key,
                Name = template.Name,
                IsSuggested = template.Key == suggestedKey,
                Note = template.Note,
                Days = template.Days
                    .Select(day => new WorkoutTemplateDayDto
                    {
                        Name = day.Name,
                        // Tip vežbe se čita iz kataloga, a ne iz baze: katalog je izvor
                        // iz kojeg se baza i puni, tu ga proveravaju testovi, i nema
                        // tihog svrstavanja nepoznatog naziva u izolacije.
                        Exercises = SessionComposition.ForLevel(
                            day.Exercises,
                            ExerciseCatalog.IsCompound,
                            experienceLevel)
                    })
                    .ToList()
            })
            .ToList();
    }
}
