using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Mesocycles;

public class MesocycleGenerator : IMesocycleGenerator
{
    private const int DurationWeeks = 4;
    private const int DefaultTargetSets = 3;

    private static readonly int[] ThreeDayOffsets = [0, 2, 4];
    private static readonly int[] FourDayOffsets = [0, 1, 3, 4];

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
            .Where(exercise => exerciseNames.Contains(exercise.Name))
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

        var exerciseIds = exercises.Select(exercise => exercise.Id).ToList();
        var latestOneRepMaxRecords = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .OrderByDescending(record => record.RecordedAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken);
        var oneRepMaxByExerciseId = latestOneRepMaxRecords
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(group => group.Key, group => group.First().ValueKg);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

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
            oneRepMaxByExerciseId);

        _db.Mesocycles.Add(mesocycle);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var exerciseNameById = exercises.ToDictionary(exercise => exercise.Id, exercise => exercise.Name);
        return ToDto(mesocycle, exerciseNameById);
    }

    private Mesocycle BuildMesocycle(
        Guid userId,
        string name,
        Goal goal,
        DateTime startDate,
        WorkoutTemplate template,
        GoalSettings goalSettings,
        IReadOnlyDictionary<string, Exercise> exerciseByName,
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId)
    {
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
                ? Math.Max(1, (int)Math.Ceiling(DefaultTargetSets / 2m))
                : DefaultTargetSets;
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

                for (var exerciseIndex = 0; exerciseIndex < templateDay.Exercises.Count; exerciseIndex++)
                {
                    var exercise = exerciseByName[templateDay.Exercises[exerciseIndex]];
                    var targetWeightKg = GetInitialTargetWeight(
                        weekNumber,
                        exercise.Id,
                        goalSettings,
                        oneRepMaxByExerciseId);

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
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId)
    {
        if (weekNumber != 1 || !oneRepMaxByExerciseId.TryGetValue(exerciseId, out var oneRepMax))
        {
            return null;
        }

        return _e1RmCalculator.WorkingWeightFor(
            oneRepMax,
            goalSettings.RepRangeMin,
            goalSettings.TargetRir);
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
        var offset = daysPerWeek switch
        {
            3 => ThreeDayOffsets[dayIndex],
            4 => FourDayOffsets[dayIndex],
            _ => dayIndex
        };

        return weekStartDate.AddDays(offset);
    }

    private static MesocycleDto ToDto(Mesocycle mesocycle, IReadOnlyDictionary<Guid, string> exerciseNameById)
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
                    Sessions = week.Sessions
                        .OrderBy(session => session.Date)
                        .Select(session => new WorkoutSessionDto
                        {
                            Id = session.Id,
                            WeekNumber = week.WeekNumber,
                            IsDeload = week.IsDeload,
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
                                    TargetWeightKg = plan.TargetWeightKg
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
