using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Mesocycles;

public class MesocycleGenerator : IMesocycleGenerator
{
    private const int DurationWeeks = 4;

    private readonly AppDbContext _db;
    private readonly E1RmCalculator _e1RmCalculator = new();

    public MesocycleGenerator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MesocycleDto> GenerateAsync(
        Guid userId,
        GenerateMesocycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = WorkoutTemplateCatalog.GetByKey(request.TemplateKey)
            ?? throw new MesocycleGenerationException($"Unknown workout template: '{request.TemplateKey}'.");

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new MesocycleGenerationException("Mesocycle name is required.");
        }

        var goalSettings = GetGoalSettings(request.Goal);
        var exerciseNames = template.Days
            .SelectMany(day => day.Exercises)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exercises = await _db.Exercises
            .AsNoTracking()
            .Where(exercise => exerciseNames.Contains(exercise.Name)
                               && (!exercise.IsCustom || exercise.CreatedByUserId == userId))
            .ToListAsync(cancellationToken);

        var exerciseByName = exercises.ToDictionary(
            exercise => exercise.Name,
            StringComparer.OrdinalIgnoreCase);
        var missingExercises = exerciseNames
            .Where(exerciseName => !exerciseByName.ContainsKey(exerciseName))
            .ToList();

        if (missingExercises.Count > 0)
        {
            throw new MesocycleGenerationException(
                $"Template references exercises missing from seed: {string.Join(", ", missingExercises)}.");
        }

        // Nivo iskustva određuje i koliko vežbi trening nosi i koliko serija svaka.
        // Profil ga prikuplja pri registraciji; do sada se nigde nije čitao.
        var experienceLevel = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;

        var exerciseIds = exercises.Select(exercise => exercise.Id).ToList();
        var weightStepByExerciseId = await WeightStepResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);
        var oneRepMaxRecords = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .OrderByDescending(record => record.RecordedAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken);

        // Najbolji 1RM u skorašnjem prozoru, ne najnoviji: poslednji zapis može
        // biti sa slabijeg dana pa bi novi ciklus krenuo preblago. Ako u prozoru
        // nema ničega, uzmi najnoviji zapis ikada.
        var lookbackCutoff = DateTime.UtcNow.AddDays(-TrainingConstants.OneRepMaxLookbackDays);
        var oneRepMaxByExerciseId = oneRepMaxRecords
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var recent = group.Where(record => record.RecordedAt >= lookbackCutoff).ToList();
                    return recent.Count > 0
                        ? recent.Max(record => record.ValueKg)
                        : group.First().ValueKg;
                });

        // Kada generator radi unutar već otvorene transakcije (npr. pri automatskom
        // prelasku na sledeći blok dugoročnog plana), ne otvara svoju — inače bi
        // ugnježdena transakcija pukla, a i prelazak mora da deli sudbinu sa
        // završetkom treninga koji ga je pokrenuo.
        // await using i na null-u je legalan no-op, pa se transakcija oslobadja i kada
        // se izadje izuzetkom.
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var activeMesocycles = await _db.Mesocycles
            .Where(mesocycle => mesocycle.UserId == userId && mesocycle.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var activeMesocycle in activeMesocycles)
        {
            activeMesocycle.IsActive = false;
        }

        var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
        var mesocycle = BuildMesocycle(
            userId,
            name,
            request.Goal,
            startDate,
            template,
            goalSettings,
            exerciseByName,
            oneRepMaxByExerciseId,
            weightStepByExerciseId,
            experienceLevel);

        _db.Mesocycles.Add(mesocycle);
        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var exerciseNameById = exercises.ToDictionary(exercise => exercise.Id, exercise => exercise.Name);
        return ToDto(mesocycle, exerciseNameById, weightStepByExerciseId);
    }

    private Mesocycle BuildMesocycle(
        Guid userId,
        string name,
        Goal goal,
        DateTime startDate,
        WorkoutTemplate template,
        GoalSettings goalSettings,
        IReadOnlyDictionary<string, Exercise> exerciseByName,
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId,
        IReadOnlyDictionary<Guid, decimal> weightStepByExerciseId,
        ExperienceLevel experienceLevel)
    {
        var startingSets = ExperienceProgramming.StartingSetsPerExercise(experienceLevel);
        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Goal = goal,
            StartDate = startDate,
            DurationWeeks = DurationWeeks,
            IsActive = true
        };

        for (var weekNumber = 1; weekNumber <= DurationWeeks; weekNumber++)
        {
            var isDeload = weekNumber == DurationWeeks;
            var targetSets = isDeload
                ? Math.Max(1, (int)Math.Ceiling(startingSets / 2m))
                : startingSets;
            var week = new TrainingWeek
            {
                Id = Guid.NewGuid(),
                WeekNumber = weekNumber,
                IsDeload = isDeload
            };

            for (var dayIndex = 0; dayIndex < template.Days.Count; dayIndex++)
            {
                var templateDay = template.Days[dayIndex];
                var session = new WorkoutSession
                {
                    Id = Guid.NewGuid(),
                    DayLabel = templateDay.Name,
                    Date = GetSessionDate(startDate, weekNumber, dayIndex, template.Days.Count),
                    Status = SessionStatus.Planned
                };

                // Šablon nudi pun spisak; nivo iskustva bira koliko i kojih vežbi ulazi
                // u trening — početnik dobija manje vežbi težište na složenima, napredni
                // jednu složenu i više izolacija.
                var dayExercises = SessionComposition.ForLevel(
                    templateDay.Exercises.Select(name => exerciseByName[name]).ToList(),
                    exercise => exercise.Type == ExerciseType.Compound,
                    experienceLevel);

                for (var exerciseIndex = 0; exerciseIndex < dayExercises.Count; exerciseIndex++)
                {
                    var exercise = dayExercises[exerciseIndex];
                    var targetWeightKg = GetInitialTargetWeight(
                        weekNumber,
                        exercise.Id,
                        goalSettings,
                        oneRepMaxByExerciseId,
                        WeightStepResolver.StepFor(weightStepByExerciseId, exercise.Id));

                    session.ExercisePlans.Add(new ExercisePlan
                    {
                        Id = Guid.NewGuid(),
                        ExerciseId = exercise.Id,
                        Order = exerciseIndex + 1,
                        TargetSets = targetSets,
                        RepRangeMin = goalSettings.RepRangeMin,
                        RepRangeMax = goalSettings.RepRangeMax,
                        TargetRir = goalSettings.TargetRir,
                        TargetWeightKg = targetWeightKg
                    });
                }

                week.Sessions.Add(session);
            }

            mesocycle.Weeks.Add(week);
        }

        return mesocycle;
    }

    private decimal? GetInitialTargetWeight(
        int weekNumber,
        Guid exerciseId,
        GoalSettings goalSettings,
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId,
        decimal weightStepKg)
    {
        if (weekNumber != 1 || !oneRepMaxByExerciseId.TryGetValue(exerciseId, out var oneRepMax))
        {
            return null;
        }

        return _e1RmCalculator.WorkingWeightFor(
            oneRepMax,
            goalSettings.RepRangeMin,
            goalSettings.TargetRir,
            weightStepKg);
    }

    private static GoalSettings GetGoalSettings(Goal goal)
    {
        return goal switch
        {
            Goal.Strength => new GoalSettings(RepRangeMin: 3, RepRangeMax: 6, TargetRir: 2),
            Goal.Hypertrophy => new GoalSettings(RepRangeMin: 8, RepRangeMax: 12, TargetRir: 1),
            _ => throw new MesocycleGenerationException($"Unsupported goal: '{goal}'.")
        };
    }

    private static DateTime GetSessionDate(DateTime startDate, int weekNumber, int dayIndex, int daysPerWeek)
    {
        var weekStartDate = startDate.AddDays((weekNumber - 1) * 7);

        return weekStartDate.AddDays(TrainingWeekSchedule.OffsetFor(daysPerWeek, dayIndex));
    }

    private static MesocycleDto ToDto(
        Mesocycle mesocycle,
        IReadOnlyDictionary<Guid, string> exerciseNameById,
        IReadOnlyDictionary<Guid, decimal> weightStepByExerciseId)
    {
        return new MesocycleDto
        {
            Id = mesocycle.Id,
            Name = mesocycle.Name,
            Goal = mesocycle.Goal,
            StartDate = mesocycle.StartDate,
            DurationWeeks = mesocycle.DurationWeeks,
            IsActive = mesocycle.IsActive,
            Weeks = mesocycle.Weeks
                .OrderBy(week => week.WeekNumber)
                .Select(week => new TrainingWeekDto
                {
                    Id = week.Id,
                    WeekNumber = week.WeekNumber,
                    IsDeload = week.IsDeload,
                    IsAutoDeload = week.IsAutoDeload,
                    FatigueScore = week.FatigueScore,
                    Sessions = week.Sessions
                        .OrderBy(session => session.Date)
                        .Select(session => new WorkoutSessionDto
                        {
                            Id = session.Id,
                            WeekNumber = week.WeekNumber,
                            IsDeload = week.IsDeload,
                            IsAutoDeload = week.IsAutoDeload,
                            DayLabel = session.DayLabel,
                            Date = session.Date,
                            Status = session.Status.ToString(),
                            ExercisePlans = session.ExercisePlans
                                .OrderBy(plan => plan.Order)
                                .Select(plan => new ExercisePlanDto
                                {
                                    Id = plan.Id,
                                    ExerciseId = plan.ExerciseId,
                                    ExerciseName = exerciseNameById.GetValueOrDefault(plan.ExerciseId, string.Empty),
                                    Order = plan.Order,
                                    TargetSets = plan.TargetSets,
                                    RepRangeMin = plan.RepRangeMin,
                                    RepRangeMax = plan.RepRangeMax,
                                    TargetRir = plan.TargetRir,
                                    TargetWeightKg = plan.TargetWeightKg,
                                    WeightStepKg = WeightStepResolver.StepFor(
                                        weightStepByExerciseId,
                                        plan.ExerciseId)
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private sealed record GoalSettings(int RepRangeMin, int RepRangeMax, int TargetRir);
}
