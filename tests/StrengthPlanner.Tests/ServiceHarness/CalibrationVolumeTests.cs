using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Kalibraciona serija (A1) je otkaz koji je plan propisao. Granice volumena udeo otkaza
/// čitaju kao umor (MRV pada od 0.25 naviše), pa ona ne sme da uđe u taj udeo - ali samo
/// ona: ista serija do otkaza u nedelji koja nije poslednja trenažna jeste znak umora.
/// </summary>
public class CalibrationVolumeTests
{
    [Fact]
    public async Task ThePrescribedFailure_DoesNotCountAsFatigue_InTheLastTrainingWeek()
    {
        using var database = new SqliteDatabase();
        var (quads, weekIds) = await SeedAsync(database, ExperienceLevel.Advanced);

        await using var context = database.NewContext();
        var service = new VolumeLandmarkService(context);
        var lastTrainingWeek = await service.GetWeeklyResponsesAsync(database.UserId, weekIds[3], CancellationToken.None);
        var earlierWeek = await service.GetWeeklyResponsesAsync(database.UserId, weekIds[2], CancellationToken.None);

        Assert.Equal(0m, lastTrainingWeek[quads].FailureShare);
        Assert.Equal(1m / 3m, earlierWeek[quads].FailureShare);
    }

    /// <summary>
    /// Ekran treninga dobija oznaku od servera: samo prva vežba treninga u poslednjoj
    /// trenažnoj nedelji. Bez ovoga je vraćanje oznake u DTO-u ostavljalo ceo paket zelen.
    /// </summary>
    [Fact]
    public async Task TheWorkoutScreen_MarksOnlyTheFirstLift_OfTheLastTrainingWeek()
    {
        using var database = new SqliteDatabase();
        var (_, weekIds) = await SeedAsync(database, ExperienceLevel.Advanced);

        await using var context = database.NewContext();
        var landmarks = new VolumeLandmarkService(context);
        var service = new SessionService(
            context,
            landmarks,
            new DeloadService(context, landmarks),
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)),
            null!);

        var lastWeek = await service.GetByIdAsync(database.UserId, SessionOf(context, weekIds[3]), CancellationToken.None);
        var earlierWeek = await service.GetByIdAsync(database.UserId, SessionOf(context, weekIds[2]), CancellationToken.None);

        Assert.Equal([true, false], lastWeek.ExercisePlans.Select(plan => plan.IsCalibration));
        Assert.All(earlierWeek.ExercisePlans, plan => Assert.False(plan.IsCalibration));
    }

    private static Guid SessionOf(StrengthPlanner.Infrastructure.Persistence.AppDbContext context, Guid weekId)
    {
        return context.WorkoutSessions.Single(session => session.TrainingWeekId == weekId).Id;
    }

    /// <summary>Početnik nema kalibraciju, pa je njegov otkaz i u poslednjoj nedelji otkaz.</summary>
    [Fact]
    public async Task ABeginnersFailure_StillCounts()
    {
        using var database = new SqliteDatabase();
        var (quads, weekIds) = await SeedAsync(database, ExperienceLevel.Beginner);

        await using var context = database.NewContext();
        var responses = await new VolumeLandmarkService(context).GetWeeklyResponsesAsync(
            database.UserId,
            weekIds[3],
            CancellationToken.None);

        Assert.Equal(1m / 3m, responses[quads].FailureShare);
    }

    /// <summary>
    /// Blok snage od četiri nedelje, deload u četvrtoj. U nedeljama 2 i 3 je isti čučanj:
    /// prva serija 8 ponavljanja do otkaza, pa dve serije 6 uz RIR 2, i iza njega još jedna
    /// složena vežba bez upisanih serija.
    /// </summary>
    private static async Task<(Guid Quads, Dictionary<int, Guid> WeekIds)> SeedAsync(
        SqliteDatabase database,
        ExperienceLevel level)
    {
        var quads = Guid.NewGuid();
        var squat = Guid.NewGuid();
        var weekIds = new Dictionary<int, Guid>();

        await using var db = database.NewContext();
        db.MuscleGroups.Add(new MuscleGroup { Id = quads, Name = "Quads" });
        db.Exercises.Add(new Exercise
        {
            Id = squat,
            Name = "Back Squat",
            Type = ExerciseType.Compound,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            SuitsLowReps = true,
            Muscles = [new ExerciseMuscle { MuscleGroupId = quads, Contribution = 1.0m }]
        });

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Strength,
            ExperienceLevel = level,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        for (var weekNumber = 1; weekNumber <= 4; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 4 };
            weekIds[weekNumber] = week.Id;
            mesocycle.Weeks.Add(week);

            if (weekNumber is 2 or 3)
            {
                var session = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = "Day A", Status = SessionStatus.Completed };
                var plan = new ExercisePlan
                {
                    Id = Guid.NewGuid(),
                    ExerciseId = squat,
                    Order = 1,
                    TargetSets = 3,
                    PrescribedSets = 3,
                    RepRangeMin = 3,
                    RepRangeMax = 6,
                    BaseRepRangeMin = 3,
                    BaseRepRangeMax = 6,
                    TargetRir = 2,
                    TargetWeightKg = 100m
                };
                plan.SetLogs.Add(Set(1, reps: 8, rir: 0, isFailure: true));
                plan.SetLogs.Add(Set(2, reps: 6, rir: 2, isFailure: false));
                plan.SetLogs.Add(Set(3, reps: 6, rir: 2, isFailure: false));
                session.ExercisePlans.Add(plan);

                // Druga složena vežba istog treninga: kalibracija je samo prva.
                session.ExercisePlans.Add(new ExercisePlan
                {
                    Id = Guid.NewGuid(),
                    ExerciseId = squat,
                    Order = 2,
                    TargetSets = 3,
                    PrescribedSets = 3,
                    RepRangeMin = 3,
                    RepRangeMax = 6,
                    BaseRepRangeMin = 3,
                    BaseRepRangeMax = 6,
                    TargetRir = 2,
                    TargetWeightKg = 90m
                });
                week.Sessions.Add(session);
            }
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return (quads, weekIds);
    }

    private static SetLog Set(int number, int reps, int rir, bool isFailure)
    {
        return new SetLog
        {
            Id = Guid.NewGuid(),
            SetNumber = number,
            WeightKg = 100m,
            Reps = reps,
            Rir = rir,
            IsFailure = isFailure,
            PerformedAt = DateTime.UtcNow
        };
    }
}
