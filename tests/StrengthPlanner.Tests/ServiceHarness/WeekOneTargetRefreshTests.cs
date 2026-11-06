using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.OneRepMax;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Nov maksimum ili test-serija preračunava opterećenja prve nedelje koja vežbač još nije
/// započeo. Do sada je test-serija posle pauze menjala napomenu na ekranu treninga, a ne i
/// težinu na šipki (pregled #99): prva nedelja je jedina čiji cilj generator upisuje.
/// </summary>
public class WeekOneTargetRefreshTests
{
    private const decimal OldTarget = 75m;

    [Fact]
    public async Task ATestSet_RewritesTheWeekOneLoadNotYetStarted_AndNothingElse()
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
        Assert.Equal(expected, await TargetOf(read, plans.WeekOnePlanned));
        Assert.NotEqual(OldTarget, expected);
        Assert.Equal(OldTarget, await TargetOf(read, plans.WeekOneStarted));
        Assert.Equal(OldTarget, await TargetOf(read, plans.WeekTwoPlanned));
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
            await TargetOf(read, plans.WeekOnePlanned));
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
        Assert.Null(await TargetOf(read, plans.WeekOnePlanned));
    }

    private static async Task<decimal?> TargetOf(StrengthPlanner.Infrastructure.Persistence.AppDbContext db, Guid planId)
    {
        return (await db.ExercisePlans.SingleAsync(plan => plan.Id == planId)).TargetWeightKg;
    }

    private sealed record SeededPlans(Guid WeekOnePlanned, Guid WeekOneStarted, Guid WeekTwoPlanned);

    /// <summary>
    /// Aktivan blok sa tri plana iste vežbe, svi sa upisanim ciljem od 75 kg (izvedenim iz
    /// maksimuma od 100 kg starog 70 dana): dan A prve nedelje koji čeka, dan B prve nedelje
    /// koji je započet, i dan A druge nedelje, koji je upisala progresija.
    /// </summary>
    private static async Task<SeededPlans> SeedBlockAsync(SqliteDatabase database, Guid exerciseId)
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
            IsActive = true
        };

        var weekOne = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 1 };
        var weekTwo = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 2 };
        mesocycle.Weeks.Add(weekOne);
        mesocycle.Weeks.Add(weekTwo);

        var planned = Plan(exerciseId);
        var started = Plan(exerciseId);
        var later = Plan(exerciseId);
        weekOne.Sessions.Add(Session("Day A", SessionStatus.Planned, planned));
        weekOne.Sessions.Add(Session("Day B", SessionStatus.InProgress, started));
        weekTwo.Sessions.Add(Session("Day A", SessionStatus.Planned, later));

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new SeededPlans(planned.Id, started.Id, later.Id);
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
