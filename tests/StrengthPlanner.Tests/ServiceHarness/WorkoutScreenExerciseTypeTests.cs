using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Ekran treninga predlaže zagrevanje i ploče samo uz složenu vežbu sa šipkom, pa mora da zna
/// tip vežbe. Oprema je ista za čučanj i za pregib (oba "Barbell"), a šipka od 20 kg važi samo
/// za prvi: bez tipa bi ekran pregibu od 25 kg predlagao ploče koje na EZ šipci ne postoje.
/// </summary>
public class WorkoutScreenExerciseTypeTests
{
    [Fact]
    public async Task TheWorkoutScreen_ReceivesTheExerciseType_OfEveryPlan()
    {
        using var database = new SqliteDatabase();
        var sessionId = await SeedAsync(database);

        await using var context = database.NewContext();
        var landmarks = new VolumeLandmarkService(context);
        var service = new SessionService(
            context,
            landmarks,
            new DeloadService(context, landmarks),
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)),
            null!);

        var session = await service.GetByIdAsync(database.UserId, sessionId, CancellationToken.None);

        // Isti "Barbell" za oba, a tip ih razlikuje.
        Assert.Equal(["Barbell", "Barbell"], session.ExercisePlans.Select(plan => plan.Equipment));
        Assert.Equal(["Compound", "Isolation"], session.ExercisePlans.Select(plan => plan.ExerciseType));
    }

    private static async Task<Guid> SeedAsync(SqliteDatabase database)
    {
        var chest = Guid.NewGuid();
        var biceps = Guid.NewGuid();
        var bench = Guid.NewGuid();
        var curl = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using var db = database.NewContext();
        db.MuscleGroups.Add(new MuscleGroup { Id = chest, Name = "Chest" });
        db.MuscleGroups.Add(new MuscleGroup { Id = biceps, Name = "Biceps" });
        db.Exercises.Add(new Exercise
        {
            Id = bench,
            Name = "Bench Press",
            Type = ExerciseType.Compound,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            SuitsLowReps = true,
            Muscles = [new ExerciseMuscle { MuscleGroupId = chest, Contribution = 1.0m }]
        });
        db.Exercises.Add(new Exercise
        {
            Id = curl,
            Name = "Barbell Curl",
            Type = ExerciseType.Isolation,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            Muscles = [new ExerciseMuscle { MuscleGroupId = biceps, Contribution = 1.0m }]
        });

        var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 1 };
        var session = new WorkoutSession { Id = sessionId, DayLabel = "Day A", Status = SessionStatus.Planned };
        session.ExercisePlans.Add(Plan(bench, order: 1));
        session.ExercisePlans.Add(Plan(curl, order: 2));
        week.Sessions.Add(session);

        db.Mesocycles.Add(new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            Weeks = [week]
        });
        await db.SaveChangesAsync();
        return sessionId;
    }

    private static ExercisePlan Plan(Guid exerciseId, int order)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = order,
            TargetSets = 3,
            PrescribedSets = 3,
            RepRangeMin = 8,
            RepRangeMax = 12,
            BaseRepRangeMin = 8,
            BaseRepRangeMax = 12,
            TargetRir = 2,
            TargetWeightKg = 40m
        };
    }
}
