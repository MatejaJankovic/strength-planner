using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Templates;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Templates;

/// <inheritdoc />
public class CustomTemplateService : ICustomTemplateService
{
    private readonly AppDbContext _db;

    public CustomTemplateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomTemplateDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var templates = await QueryFor(userId)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(ToDto).ToList();
    }

    public async Task<CustomTemplateDto> GetByIdAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await QueryFor(userId)
            .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken)
            ?? throw new MesocycleGenerationException("Šablon ne postoji.");

        return ToDto(template);
    }

    public async Task<CustomTemplateDto> CreateAsync(
        Guid userId,
        SaveCustomTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = await _db.UserWorkoutTemplates
            .CountAsync(template => template.UserId == userId, cancellationToken);

        if (count >= TrainingConstants.MaxTemplatesPerUser)
        {
            throw new MesocycleGenerationException(
                $"Više od {TrainingConstants.MaxTemplatesPerUser} ličnih šablona nije dozvoljeno. "
                + "Obriši neki pa pokušaj ponovo.");
        }

        var name = await ValidateAsync(userId, request, cancellationToken);

        var template = new UserWorkoutTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        FillDays(template, request);

        _db.UserWorkoutTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(userId, template.Id, cancellationToken);
    }

    public async Task<CustomTemplateDto> UpdateAsync(
        Guid userId,
        Guid templateId,
        SaveCustomTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = await _db.UserWorkoutTemplates
            .Include(template => template.Days)
                .ThenInclude(day => day.Exercises)
            .FirstOrDefaultAsync(
                template => template.Id == templateId && template.UserId == userId,
                cancellationToken)
            ?? throw new MesocycleGenerationException("Šablon ne postoji.");

        var name = await ValidateAsync(userId, request, cancellationToken);

        template.Name = name;

        // Dani se zamenjuju u celosti. Spajanje po rednom broju bi štedelo par redova, a
        // koštalo bi tačnosti: izmena reda vežbi bi ostavila stare veze i menjala značenje
        // rednog broja.
        _db.UserWorkoutTemplateDays.RemoveRange(template.Days);
        template.Days.Clear();
        FillDays(template, request);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(userId, template.Id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.UserWorkoutTemplates
            .FirstOrDefaultAsync(
                template => template.Id == templateId && template.UserId == userId,
                cancellationToken)
            ?? throw new MesocycleGenerationException("Šablon ne postoji.");

        // Već generisan mezociklus ne zavisi od šablona - vežbe su prepisane u plan. Blok
        // koji tek čeka svoj red zavisi, jer se generiše iz ključa tek tada.
        var key = CustomTemplateKey.For(templateId);
        var isWaitedOn = await _db.MacrocycleBlocks
            .AnyAsync(
                block => block.TemplateKey == key
                         && block.MesocycleId == null
                         && block.Macrocycle.UserId == userId,
                cancellationToken);

        if (isWaitedOn)
        {
            throw new MesocycleGenerationException(
                "Šablon koristi blok dugoročnog plana koji još nije generisan. "
                + "Obriši taj plan ili sačekaj da blok bude odrađen.");
        }

        _db.UserWorkoutTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<UserWorkoutTemplate> QueryFor(Guid userId)
    {
        return _db.UserWorkoutTemplates
            .AsNoTracking()
            .Include(template => template.Days)
                .ThenInclude(day => day.Exercises)
                    .ThenInclude(exercise => exercise.Exercise)
            .Where(template => template.UserId == userId);
    }

    /// <summary>
    /// Provere koje anotacije na DTO-u ne mogu: da vežbe postoje i da su dostupne baš ovom
    /// korisniku, i da donja granica opsega nije iznad gornje.
    /// </summary>
    private async Task<string> ValidateAsync(
        Guid userId,
        SaveCustomTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new MesocycleGenerationException("Unesi naziv šablona.");
        }

        foreach (var day in request.Days)
        {
            if (day.Name.Trim().Length == 0)
            {
                throw new MesocycleGenerationException("Svaki dan mora da ima naziv.");
            }

            foreach (var exercise in day.Exercises)
            {
                if (exercise.RepRangeMin > exercise.RepRangeMax)
                {
                    throw new MesocycleGenerationException(
                        "Donja granica ponavljanja ne sme da bude veća od gornje.");
                }
            }

            // Ista vežba dvaput u istom danu nije samo neuredna. Automatski deload izvodi
            // polazni broj serija po paru (naziv dana, vežba) i uzima prvi pogodak, pa bi
            // drugi unos dobio tuđu vrednost.
            var duplicateExercise = day.Exercises
                .GroupBy(exercise => exercise.ExerciseId)
                .Any(group => group.Count() > 1);

            if (duplicateExercise)
            {
                throw new MesocycleGenerationException(
                    $"Dan \"{day.Name.Trim()}\" sadrži istu vežbu dvaput.");
            }
        }

        // Naziv dana postaje DayLabel treninga, a deload po njemu prepoznaje dan. Dva dana
        // istog naziva bi značila da se polazni broj serija čita iz pogrešnog treninga.
        var duplicateDay = request.Days
            .GroupBy(day => day.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateDay is not null)
        {
            throw new MesocycleGenerationException(
                $"Dva dana nose isti naziv (\"{duplicateDay.Key}\"). Nazivi dana moraju da se razlikuju.");
        }

        var exerciseIds = request.Days
            .SelectMany(day => day.Exercises)
            .Select(exercise => exercise.ExerciseId)
            .Distinct()
            .ToList();

        // Filter vlasništva nad Exercise već propušta samo sistemske vežbe i one koje je
        // ovaj korisnik napravio, pa tuđa custom vežba ovde ispadne kao nepostojeća.
        var found = await _db.Exercises
            .Where(exercise => exerciseIds.Contains(exercise.Id))
            .Select(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

        if (found.Count != exerciseIds.Count)
        {
            throw new MesocycleGenerationException("Šablon sadrži vežbu koja ne postoji.");
        }

        return name;
    }

    private static void FillDays(UserWorkoutTemplate template, SaveCustomTemplateRequest request)
    {
        for (var dayIndex = 0; dayIndex < request.Days.Count; dayIndex++)
        {
            var requestDay = request.Days[dayIndex];
            var day = new UserWorkoutTemplateDay
            {
                Id = Guid.NewGuid(),
                Name = requestDay.Name.Trim(),
                Order = dayIndex + 1
            };

            for (var exerciseIndex = 0; exerciseIndex < requestDay.Exercises.Count; exerciseIndex++)
            {
                var requestExercise = requestDay.Exercises[exerciseIndex];
                day.Exercises.Add(new UserWorkoutTemplateExercise
                {
                    Id = Guid.NewGuid(),
                    ExerciseId = requestExercise.ExerciseId,
                    Order = exerciseIndex + 1,
                    Sets = requestExercise.Sets,
                    RepRangeMin = requestExercise.RepRangeMin,
                    RepRangeMax = requestExercise.RepRangeMax
                });
            }

            template.Days.Add(day);
        }
    }

    private static CustomTemplateDto ToDto(UserWorkoutTemplate template)
    {
        return new CustomTemplateDto
        {
            Id = template.Id,
            Key = CustomTemplateKey.For(template.Id),
            Name = template.Name,
            Days = template.Days
                .OrderBy(day => day.Order)
                .Select(day => new CustomTemplateDayDto
                {
                    Name = day.Name,
                    Exercises = day.Exercises
                        .OrderBy(exercise => exercise.Order)
                        .Select(exercise => new CustomTemplateExerciseDto
                        {
                            ExerciseId = exercise.ExerciseId,
                            ExerciseName = exercise.Exercise.Name,
                            Sets = exercise.Sets,
                            RepRangeMin = exercise.RepRangeMin,
                            RepRangeMax = exercise.RepRangeMax
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
