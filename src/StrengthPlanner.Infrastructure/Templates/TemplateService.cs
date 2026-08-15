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
///
/// Lični šabloni idu kroz isti spisak, ali <b>neskraćeni</b>: njih je korisnik sastavio
/// vežbu po vežbu, pa se prikazuju onako kako će i biti odrađeni.
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
        var experienceLevel = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;

        var builtIn = WorkoutTemplateCatalog
            .GetAll()
            .Select(template => new WorkoutTemplateDto
            {
                Key = template.Key,
                Name = template.Name,
                IsCustom = false,
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

        var custom = await _db.UserWorkoutTemplates
            .AsNoTracking()
            .Include(template => template.Days)
                .ThenInclude(day => day.Exercises)
                    .ThenInclude(exercise => exercise.Exercise)
            .Where(template => template.UserId == userId)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);

        // Lični šabloni idu prvi: korisnik koji ih je napravio traži njih, a ne katalog.
        return custom
            .Select(template => new WorkoutTemplateDto
            {
                Key = CustomTemplateKey.For(template.Id),
                Name = template.Name,
                IsCustom = true,
                Note = null,
                Days = template.Days
                    .OrderBy(day => day.Order)
                    .Select(day => new WorkoutTemplateDayDto
                    {
                        Name = day.Name,
                        Exercises = day.Exercises
                            .OrderBy(exercise => exercise.Order)
                            .Select(exercise => exercise.Exercise.Name)
                            .ToList()
                    })
                    .ToList()
            })
            .Concat(builtIn)
            .ToList();
    }
}
