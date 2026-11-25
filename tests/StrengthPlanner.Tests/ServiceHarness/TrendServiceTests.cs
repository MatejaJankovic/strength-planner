using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Tonnage and weekly volume were read one block at a time. The trend reads every set the user
/// ever logged by calendar week, across plans and blocks, and the service has to keep each of those
/// things straight: whose sets, which range, which block, and what a muscle is credited with.
///
/// The service reads the clock, so the sets are placed relative to now, and the weeks are looked
/// up by the Monday they start on.
/// </summary>
public class TrendServiceTests
{
    private static DateTime ThisMonday => WeeklyTrend.WeekStartOf(DateTime.UtcNow);

    /// <summary>
    /// Four weeks back, a hypertrophy block: a week of three sets and a deload week of one. Two
    /// weeks back a strength block starts: two sets of the same press, one of which is a pull-up
    /// carrying body mass.
    /// </summary>
    [Fact]
    public async Task TheTrend_GroupsByCalendarWeek_AcrossBlocks_AndMarksTheBoundaryAndTheDeload()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var trend = await GetAsync(database, weeks: 5);

        // The week before the first set is not shown; the current week, with nothing yet, is.
        Assert.Equal(4, trend.Weeks.Count);
        Assert.Equal([3, 1, 3, 0], trend.Weeks.Select(week => week.Sets));

        // Week -3: three sets of 100 x 5 of the first block. Week -2: one deload set. Week -1: three
        // sets of the strength block.
        Assert.Equal(1500m, trend.Weeks[0].TonnageKg);
        Assert.Equal("Blok 1", trend.Weeks[0].BlockName);
        Assert.Equal("Plan jesen", trend.Weeks[0].PlanName);
        Assert.True(trend.Weeks[0].BlockStarts);
        Assert.False(trend.Weeks[0].IsDeload);

        Assert.True(trend.Weeks[1].IsDeload);
        Assert.False(trend.Weeks[1].BlockStarts);

        Assert.Equal("Blok 2", trend.Weeks[2].BlockName);
        Assert.True(trend.Weeks[2].BlockStarts);
        Assert.Equal(0, trend.Weeks[3].Sets);
        Assert.Null(trend.Weeks[3].BlockName);
    }

    /// <summary>Tonnage carries the body mass of a movement, as it does everywhere else.</summary>
    [Fact]
    public async Task Tonnage_CountsTheBodyMassOfABodyweightMovement()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var trend = await GetAsync(database, weeks: 5);

        // Week -1: 100 x 5 and 100 x 5 of the press, and a pull-up with 5 kg on a body portion of 80, 6 reps.
        Assert.Equal(100m * 5 * 2 + 85m * 6, trend.Weeks[2].TonnageKg);
    }

    [Fact]
    public async Task AMuscle_TakesItsShareOfEverySet_AndOnlyTrainedMusclesAreListed()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var trend = await GetAsync(database, weeks: 5);

        Assert.Equal(["Back", "Chest", "Triceps"], trend.Muscles);
        // Week -3: three presses, each with a full chest credit and half for the triceps.
        Assert.Equal(3m, trend.Weeks[0].StimulativeSetsByMuscle["Chest"]);
        Assert.Equal(1.5m, trend.Weeks[0].StimulativeSetsByMuscle["Triceps"]);
        Assert.DoesNotContain("Back", trend.Weeks[0].StimulativeSetsByMuscle.Keys);
        // Week -1: two presses and a pull-up that trains the back.
        Assert.Equal(1m, trend.Weeks[2].StimulativeSetsByMuscle["Back"]);
    }

    /// <summary>
    /// A set far from failure is not volume: five in reserve counts nothing, four counts half, the
    /// same as on the weekly volume screen, so the two screens do not disagree about a week.
    /// </summary>
    [Fact]
    public async Task TheTotal_ReadsTheSameCreditAsTheVolumeScreen()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, lastWeekRir: [1, 4, 5]);

        var trend = await GetAsync(database, weeks: 5);

        Assert.Equal(1.5m, trend.Weeks[2].StimulativeSets);
        Assert.Equal(3, trend.Weeks[2].Sets);
    }

    [Fact]
    public async Task ARange_ShowsOnlyItsOwnWeeks_AndAllHistoryShowsEverything()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var two = await GetAsync(database, weeks: 2);
        var all = await GetAsync(database, weeks: null);

        Assert.Equal(2, two.Weeks.Count);
        Assert.Equal(3, two.Weeks.Sum(week => week.Sets));
        // All history starts at the first set, three weeks ago, and ends this week.
        Assert.Equal(4, all.Weeks.Count);
        Assert.Equal(7, all.Weeks.Sum(week => week.Sets));
    }

    [Fact]
    public async Task AnotherUser_SeesNothingOfIt()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        await using var context = database.NewContext();
        var trend = await new AnalyticsService(context).GetTrendAsync(Guid.NewGuid(), weeks: null);

        Assert.Empty(trend.Weeks);
        Assert.Empty(trend.Muscles);
    }

    private static async Task<StrengthPlanner.Application.DTOs.Analytics.TrendDto> GetAsync(
        SqliteDatabase database,
        int? weeks)
    {
        await using var context = database.NewContext();
        return await new AnalyticsService(context).GetTrendAsync(database.UserId, weeks);
    }

    private static async Task SeedAsync(SqliteDatabase database, int[]? lastWeekRir = null)
    {
        var chest = Guid.NewGuid();
        var triceps = Guid.NewGuid();
        var back = Guid.NewGuid();
        var press = Guid.NewGuid();
        var pullUp = Guid.NewGuid();

        await using var db = database.NewContext();
        db.MuscleGroups.AddRange(
            new MuscleGroup { Id = chest, Name = "Chest" },
            new MuscleGroup { Id = triceps, Name = "Triceps" },
            new MuscleGroup { Id = back, Name = "Back" });
        db.Exercises.AddRange(
            new Exercise
            {
                Id = press,
                Name = "Bench Press",
                Type = ExerciseType.Compound,
                Equipment = "Barbell",
                Muscles =
                [
                    new ExerciseMuscle { MuscleGroupId = chest, Contribution = 1m },
                    new ExerciseMuscle { MuscleGroupId = triceps, Contribution = 0.5m }
                ]
            },
            new Exercise
            {
                Id = pullUp,
                Name = "Pull-up",
                Type = ExerciseType.Compound,
                Equipment = "Bodyweight",
                BodyweightShare = 1m,
                Muscles = [new ExerciseMuscle { MuscleGroupId = back, Contribution = 1m }]
            });

        var monday = ThisMonday;
        var first = NewBlock(database, "Blok 1", Goal.Hypertrophy, monday.AddDays(-28));
        var second = NewBlock(database, "Blok 2", Goal.Strength, monday.AddDays(-14));

        AddSets(first, weekNumber: 1, isDeload: false, press, monday.AddDays(-21).AddHours(9), rirs: [2, 2, 2], loadKg: 100m, bodyweightKg: 0m, reps: 5);
        AddSets(first, weekNumber: 2, isDeload: true, press, monday.AddDays(-14).AddHours(9), rirs: [3], loadKg: 60m, bodyweightKg: 0m, reps: 5);
        var rirs = lastWeekRir ?? [2, 2];
        AddSets(second, weekNumber: 1, isDeload: false, press, monday.AddDays(-7).AddHours(9), rirs: rirs.Length == 3 ? [rirs[0], rirs[1], rirs[2]] : rirs, loadKg: 100m, bodyweightKg: 0m, reps: 5);
        if (lastWeekRir is null)
        {
            AddSets(second, weekNumber: 1, isDeload: false, pullUp, monday.AddDays(-7).AddHours(10), rirs: [2], loadKg: 5m, bodyweightKg: 80m, reps: 6);
        }

        db.Mesocycles.AddRange(first, second);

        var plan = new Macrocycle { Id = Guid.NewGuid(), UserId = database.UserId, Name = "Plan jesen", StartDate = monday.AddDays(-28), IsActive = true };
        plan.Blocks.Add(new MacrocycleBlock { Id = Guid.NewGuid(), Order = 1, Goal = Goal.Hypertrophy, TemplateKey = "x", MesocycleId = first.Id });
        plan.Blocks.Add(new MacrocycleBlock { Id = Guid.NewGuid(), Order = 2, Goal = Goal.Strength, TemplateKey = "x", MesocycleId = second.Id });
        db.Macrocycles.Add(plan);

        await db.SaveChangesAsync();
    }

    private static Mesocycle NewBlock(SqliteDatabase database, string name, Goal goal, DateTime start)
    {
        return new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = name,
            Goal = goal,
            StartDate = start,
            IsActive = name == "Blok 2"
        };
    }

    private static void AddSets(
        Mesocycle mesocycle,
        int weekNumber,
        bool isDeload,
        Guid exerciseId,
        DateTime at,
        int[] rirs,
        decimal loadKg,
        decimal bodyweightKg,
        int reps)
    {
        var week = mesocycle.Weeks.SingleOrDefault(candidate => candidate.WeekNumber == weekNumber);
        if (week is null)
        {
            week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = isDeload };
            mesocycle.Weeks.Add(week);
        }

        var session = week.Sessions.SingleOrDefault();
        if (session is null)
        {
            session = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = "Day A", Status = SessionStatus.Completed };
            week.Sessions.Add(session);
        }

        var plan = new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = session.ExercisePlans.Count + 1,
            TargetSets = rirs.Length,
            PrescribedSets = rirs.Length,
            RepRangeMin = 3,
            RepRangeMax = 6,
            BaseRepRangeMin = 3,
            BaseRepRangeMax = 6,
            TargetRir = 2
        };

        for (var index = 0; index < rirs.Length; index++)
        {
            plan.SetLogs.Add(new SetLog
            {
                Id = Guid.NewGuid(),
                SetNumber = index + 1,
                WeightKg = loadKg,
                BodyweightLoadKg = bodyweightKg,
                Reps = reps,
                Rir = rirs[index],
                PerformedAt = at.AddMinutes(index)
            });
        }

        session.ExercisePlans.Add(plan);
    }
}
