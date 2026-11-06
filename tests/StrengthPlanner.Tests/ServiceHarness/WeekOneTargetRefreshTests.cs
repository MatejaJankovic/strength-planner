using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.OneRepMax;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Nov maksimum ili test-serija preračunava opterećenja prve nedelje koja vežbač još nije
/// dirnuo. Do sada je test-serija posle pauze menjala napomenu na ekranu treninga, a ne i
/// težinu na šipki (pregled #99): prva nedelja je jedina čiji cilj generator upisuje.
///
/// Očekivane vrednosti se računaju istim domenskim pravilom, ali sa korakom i delom telesne
/// mase koji se razlikuju od podrazumevanih (2.5 kg, 0), jer bi test koji sam unosi konstantu
/// koju aplikacija izvodi prošao i za implementaciju koja je ignoriše (runda 9).
/// </summary>
public class WeekOneTargetRefreshTests
{
    private const decimal OldTarget = 75m;

    [Fact]
    public async Task ATestSet_RewritesUntouchedWeekOneLoads_AndNothingElse()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var plans = await SeedBlockAsync(database, row);

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddFromSetAsync(
                database.UserId,
                new() { ExerciseId = row, WeightKg = 55m, Reps = 5, Rir = 1 });
        }

        // 55 kg × 5 uz RIR 1 je 66 kg, i to je jedini zapis u prozoru; stari od 100 kg je van njega.
        var expected = StartingLoad.FromOneRepMax(new E1RmCalculator(), 66m, 8, 1, 0m, 2.5m);

        await using var read = database.NewContext();
        Assert.NotEqual(OldTarget, expected);
        Assert.Equal(expected, await TargetOf(read, plans.Planned));
        Assert.Equal(expected, await TargetOf(read, plans.Skipped));
        Assert.Equal(OldTarget, await TargetOf(read, plans.Started));
        Assert.Equal(OldTarget, await TargetOf(read, plans.PlannedWithASet));
        Assert.Equal(OldTarget, await TargetOf(read, plans.WeekTwo));
    }

    [Fact]
    public async Task AManualMaximum_RewritesTheWeekOneLoadToo()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var plans = await SeedBlockAsync(database, row);

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddManualAsync(database.UserId, new() { ExerciseId = row, ValueKg = 80m });
        }

        await using var read = database.NewContext();
        Assert.Equal(
            StartingLoad.FromOneRepMax(new E1RmCalculator(), 80m, 8, 1, 0m, 2.5m),
            await TargetOf(read, plans.Planned));
    }

    [Fact]
    public async Task TheLifter_sOwnWeightStep_IsUsed()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var plans = await SeedBlockAsync(database, row);

        await using (var db = database.NewContext())
        {
            db.UserExerciseSettings.Add(new UserExerciseSetting
            {
                Id = Guid.NewGuid(),
                UserId = database.UserId,
                ExerciseId = row,
                WeightStepKg = 5m
            });
            await db.SaveChangesAsync();
        }

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddManualAsync(database.UserId, new() { ExerciseId = row, ValueKg = 81m });
        }

        var expected = StartingLoad.FromOneRepMax(new E1RmCalculator(), 81m, 8, 1, 0m, 5m);

        await using var read = database.NewContext();
        Assert.NotEqual(StartingLoad.FromOneRepMax(new E1RmCalculator(), 81m, 8, 1, 0m, 2.5m), expected);
        Assert.Equal(expected, await TargetOf(read, plans.Planned));
    }

    /// <summary>
    /// Sklek nosi 64% telesne mase: na 80 kg to je 51.2, što nije umnožak koraka od 1 kg, pa
    /// se zaokruživanje u dodatom i u ukupnom opterećenju razlikuje.
    /// </summary>
    [Fact]
    public async Task ABodyweightExercise_CountsTheBodyAndAnswersInAddedKilograms()
    {
        using var database = new SqliteDatabase();
        var pushUp = Guid.NewGuid();
        await using (var db = database.NewContext())
        {
            db.Exercises.Add(new Exercise
            {
                Id = pushUp,
                Name = "Push-up",
                Type = ExerciseType.Compound,
                Equipment = "Bodyweight",
                WeightStepKg = 1m,
                BodyweightShare = 0.64m
            });
            db.Profiles.Add(new Profile
            {
                Id = Guid.NewGuid(),
                UserId = database.UserId,
                Age = 30,
                BodyweightKg = 80m,
                ExperienceLevel = ExperienceLevel.Intermediate
            });
            await db.SaveChangesAsync();
        }

        var plans = await SeedBlockAsync(database, pushUp);

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddFromSetAsync(
                database.UserId,
                new() { ExerciseId = pushUp, WeightKg = 10m, Reps = 8, Rir = 1 });
        }

        // Ukupno opterećenje test-serije je 10 + 51.2 = 61.2 kg.
        var oneRepMax = new E1RmCalculator().EstimateOneRepMax(61.2m, 8, 1);
        var expected = StartingLoad.FromOneRepMax(new E1RmCalculator(), oneRepMax, 8, 1, 51.2m, 1m);

        await using var read = database.NewContext();
        Assert.Equal(expected, await TargetOf(read, plans.Planned));
    }

    [Fact]
    public async Task ABlockThatIsNoLongerActive_IsLeftAlone()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var plans = await SeedBlockAsync(database, row, isActive: false);

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddManualAsync(database.UserId, new() { ExerciseId = row, ValueKg = 80m });
        }

        await using var read = database.NewContext();
        Assert.Equal(OldTarget, await TargetOf(read, plans.Planned));
    }

    [Fact]
    public async Task DeletingTheOnlyMaximum_LeavesTheWeekOneLoadToBeComputedOnRead()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var plans = await SeedBlockAsync(database, row);

        Guid recordId;
        await using (var db = database.NewContext())
        {
            recordId = await db.OneRepMaxRecords.Select(record => record.Id).SingleAsync();
        }

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).DeleteAsync(database.UserId, recordId);
        }

        await using var read = database.NewContext();
        Assert.Null(await TargetOf(read, plans.Planned));
    }

    private static async Task<decimal?> TargetOf(AppDbContext db, Guid planId)
    {
        return (await db.ExercisePlans.SingleAsync(plan => plan.Id == planId)).TargetWeightKg;
    }

    private sealed record SeededPlans(Guid Planned, Guid Skipped, Guid Started, Guid PlannedWithASet, Guid WeekTwo);

    /// <summary>
    /// Blok sa pet planova iste vežbe, svi sa upisanim ciljem od 75 kg (izvedenim iz maksimuma
    /// od 100 kg starog 70 dana). Prva nedelja: dan koji čeka, preskočen dan, započet dan i
    /// dan koji čeka ali već ima upisanu seriju. Druga nedelja: dan koji je upisala progresija.
    /// </summary>
    private static async Task<SeededPlans> SeedBlockAsync(SqliteDatabase database, Guid exerciseId, bool isActive = true)
    {
        await using var db = database.NewContext();
        db.OneRepMaxRecords.Add(new OneRepMaxRecord
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            ExerciseId = exerciseId,
            ValueKg = 100m,
            Source = OneRepMaxSource.Estimated,
            RecordedAt = DateTime.UtcNow.AddDays(-70)
        });

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = isActive
        };

        var weekOne = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 1 };
        var weekTwo = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 2 };
        mesocycle.Weeks.Add(weekOne);
        mesocycle.Weeks.Add(weekTwo);

        var planned = Plan(exerciseId);
        var skipped = Plan(exerciseId);
        var started = Plan(exerciseId);
        var withASet = Plan(exerciseId);
        withASet.SetLogs.Add(new SetLog
        {
            Id = Guid.NewGuid(),
            SetNumber = 1,
            WeightKg = OldTarget,
            Reps = 8,
            PerformedAt = DateTime.UtcNow
        });
        var later = Plan(exerciseId);

        weekOne.Sessions.Add(Session("Day A", SessionStatus.Planned, planned));
        weekOne.Sessions.Add(Session("Day B", SessionStatus.Skipped, skipped));
        weekOne.Sessions.Add(Session("Day C", SessionStatus.InProgress, started));
        weekOne.Sessions.Add(Session("Day D", SessionStatus.Planned, withASet));
        weekTwo.Sessions.Add(Session("Day A", SessionStatus.Planned, later));

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new SeededPlans(planned.Id, skipped.Id, started.Id, withASet.Id, later.Id);
    }

    private static WorkoutSession Session(string dayLabel, SessionStatus status, ExercisePlan plan)
    {
        var session = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = dayLabel, Status = status };
        session.ExercisePlans.Add(plan);
        return session;
    }

    private static ExercisePlan Plan(Guid exerciseId)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = 1,
            TargetSets = 4,
            PrescribedSets = 4,
            RepRangeMin = 8,
            RepRangeMax = 12,
            BaseRepRangeMin = 8,
            BaseRepRangeMax = 12,
            TargetRir = 1,
            TargetWeightKg = OldTarget
        };
    }
}
