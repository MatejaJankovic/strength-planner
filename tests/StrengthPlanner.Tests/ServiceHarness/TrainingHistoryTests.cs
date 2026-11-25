using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// The history lists every workout that is over across all plans and blocks, newest first. A
/// block that ended used to disappear from view: analytics reads one block at a time and the
/// plan screen shows the current one.
/// </summary>
public class TrainingHistoryTests
{
    // Daleko u prošlosti: izvedeni datumi se stežu na "sada", pa bi blizak datum zavisio od sata.
    private static readonly DateTime Day1 = new(2024, 10, 5, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ItListsFinishedAndSkippedWorkouts_AcrossBlocks_NewestFirst()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database);

        var history = await GetAsync(database, page: 1, pageSize: 20);

        // Planned and in-progress workouts are not history. The older block's two workouts
        // come after the newer block's, in time order.
        Assert.Equal(5, history.Total);
        Assert.Equal(
            [seed.NewBlockSkipped, seed.NewBlockSecond, seed.NewBlockFirst, seed.OldBlockSecond, seed.OldBlockFirst],
            history.Items.Select(item => item.SessionId));
    }

    [Fact]
    public async Task AnEntry_SaysWhatWasDone()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database);

        var entry = (await GetAsync(database, 1, 20)).Items.Single(item => item.SessionId == seed.NewBlockFirst);

        Assert.Equal("Completed", entry.Status);
        Assert.Equal("Day A", entry.DayLabel);
        Assert.Equal(1, entry.WeekNumber);
        Assert.Equal("Plan jesen", entry.PlanName);
        Assert.Equal("Blok 2", entry.BlockName);
        Assert.Equal("Strength", entry.Goal);
        Assert.Equal(["Back Squat", "Pull-up"], entry.Exercises);
        Assert.Equal(3, entry.SetCount);
        // 100 x 5 twice, and a pull-up with 5 kg added on a body portion of 80: 85 x 6.
        Assert.Equal(100m * 5 * 2 + 85m * 6, entry.TonnageKg);
        Assert.True(entry.DateKnown);
        Assert.Equal(Day1.AddDays(20), entry.Date);
    }

    /// <summary>
    /// A workout finished before the completion time was stored is placed at its last set, and one
    /// that was skipped, with nothing at all to date it, at the week it was planned for, marked as
    /// not known.
    /// </summary>
    [Fact]
    public async Task AnOlderWorkout_UsesItsLastSet_AndASkippedOne_TheWeekOfItsBlock()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database);

        var items = (await GetAsync(database, 1, 20)).Items;
        var old = items.Single(item => item.SessionId == seed.OldBlockFirst);
        var skipped = items.Single(item => item.SessionId == seed.NewBlockSkipped);

        Assert.Equal(Day1, old.Date);
        Assert.True(old.DateKnown);
        Assert.Equal("Skipped", skipped.Status);
        Assert.False(skipped.DateKnown);
        Assert.Empty(skipped.Exercises);
        Assert.Equal(0, skipped.SetCount);
        Assert.Equal(0m, skipped.TonnageKg);
    }

    [Fact]
    public async Task APage_HoldsItsShare_AndTheTotalIsTheWholeHistory()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database);

        var first = await GetAsync(database, page: 1, pageSize: 2);
        var third = await GetAsync(database, page: 3, pageSize: 2);

        Assert.Equal(2, first.Items.Count);
        Assert.Equal(5, first.Total);
        Assert.Equal([seed.OldBlockFirst], third.Items.Select(item => item.SessionId));
        Assert.Empty((await GetAsync(database, page: 4, pageSize: 2)).Items);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(-3, 500, TrainingHistoryService.MaxPageSize)]
    public async Task ABadPage_IsClamped_NotRejected(int page, int pageSize, int expectedPageSize)
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var history = await GetAsync(database, page, pageSize);

        Assert.Equal(1, history.Page);
        Assert.Equal(expectedPageSize, history.PageSize);
        Assert.NotEmpty(history.Items);
    }

    /// <summary>
    /// A page number so large that (page - 1) x pageSize overflows an int used to wrap to a
    /// negative offset, and a negative Skip returns the first page instead of an empty one.
    /// The page is chosen so that the product wraps to a NEGATIVE int: 42,949,673 x 50 is
    /// 2,147,483,650, just over int.MaxValue. A larger page such as 100,000,000 wraps to a
    /// positive number and would pass for the old code too.
    /// </summary>
    [Fact]
    public async Task AHugePage_IsPastTheEnd_NotTheFirstPage()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        var history = await GetAsync(database, page: 42_949_674, pageSize: 50);

        Assert.Empty(history.Items);
        Assert.Equal(5, history.Total);
    }

    /// <summary>
    /// A workout finished before completion times were stored is dated by its last set. A set
    /// added while correcting it must not be stamped with now, or the old workout would move to
    /// today and to the top of the history.
    /// </summary>
    [Fact]
    public async Task AddingASet_ToAnOldWorkout_DoesNotMoveItToToday()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database);
        Guid planId;
        await using (var db = database.NewContext())
        {
            // The same day of the next week is still to be done, so the workout can be corrected.
            (await db.WorkoutSessions.SingleAsync(session => session.Id == seed.OldBlockSecond)).Status = SessionStatus.Planned;
            await db.SaveChangesAsync();
            planId = await db.ExercisePlans
                .Where(plan => plan.WorkoutSessionId == seed.OldBlockFirst)
                .Select(plan => plan.Id)
                .FirstAsync();
        }

        await using (var context = database.NewContext())
        {
            await new SetLogService(context).AddSetAsync(
                database.UserId,
                planId,
                new StrengthPlanner.Application.DTOs.SetLogs.AddSetLogRequest { WeightKg = 100m, Reps = 5, Rir = 2 });
        }

        var history = await GetAsync(database, 1, 20);
        var old = history.Items.Single(item => item.SessionId == seed.OldBlockFirst);

        Assert.Equal(Day1, old.Date);
        Assert.Equal(seed.OldBlockFirst, history.Items[^1].SessionId);
        Assert.Equal(3, old.SetCount);
        Assert.True(old.DateKnown);
    }

    [Fact]
    public async Task AnotherUser_SeesNothingOfIt()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database);

        await using var context = database.NewContext();
        var history = await new TrainingHistoryService(context).GetAsync(Guid.NewGuid(), 1, 20);

        Assert.Empty(history.Items);
        Assert.Equal(0, history.Total);
    }

    private static async Task<StrengthPlanner.Application.DTOs.Sessions.TrainingHistoryDto> GetAsync(
        SqliteDatabase database,
        int page,
        int pageSize)
    {
        await using var context = database.NewContext();
        return await new TrainingHistoryService(context).GetAsync(database.UserId, page, pageSize);
    }

    private sealed record Seed(
        Guid OldBlockFirst,
        Guid OldBlockSecond,
        Guid NewBlockFirst,
        Guid NewBlockSecond,
        Guid NewBlockSkipped);

    /// <summary>
    /// One plan, two blocks. The old block has two finished workouts, both before the completion
    /// time was stored (only their sets date them). The new block has two finished ones with a
    /// completion time, a skipped one with no date at all, and one still planned.
    /// </summary>
    private static async Task<Seed> SeedAsync(SqliteDatabase database)
    {
        var squat = Guid.NewGuid();
        var pullUp = Guid.NewGuid();

        await using var db = database.NewContext();
        db.Exercises.AddRange(
            new Exercise { Id = squat, Name = "Back Squat", Type = ExerciseType.Compound, Equipment = "Barbell" },
            new Exercise { Id = pullUp, Name = "Pull-up", Type = ExerciseType.Compound, Equipment = "Bodyweight", BodyweightShare = 1m });

        var oldBlock = NewMesocycle(database, "Blok 1", Day1.AddDays(-2));
        var newBlock = NewMesocycle(database, "Blok 2", Day1.AddDays(14));

        var oldFirst = AddSession(oldBlock, 1, "Day A", SessionStatus.Completed, completedAt: null, setAt: Day1, squat);
        var oldSecond = AddSession(oldBlock, 2, "Day A", SessionStatus.Completed, completedAt: null, setAt: Day1.AddDays(7), squat);

        var newFirst = AddSession(newBlock, 1, "Day A", SessionStatus.Completed, Day1.AddDays(20), setAt: Day1.AddDays(20), squat, pullUp);
        var newSecond = AddSession(newBlock, 2, "Day A", SessionStatus.Completed, Day1.AddDays(27), setAt: Day1.AddDays(27), squat);
        var skipped = AddSession(newBlock, 3, "Day A", SessionStatus.Skipped, completedAt: null, setAt: null);
        AddSession(newBlock, 4, "Day A", SessionStatus.Planned, completedAt: null, setAt: null);
        AddSession(newBlock, 4, "Day B", SessionStatus.InProgress, completedAt: null, setAt: null);

        db.Mesocycles.AddRange(oldBlock, newBlock);

        var plan = new Macrocycle { Id = Guid.NewGuid(), UserId = database.UserId, Name = "Plan jesen", StartDate = Day1, IsActive = true };
        plan.Blocks.Add(new MacrocycleBlock { Id = Guid.NewGuid(), Order = 1, Goal = Goal.Hypertrophy, TemplateKey = "x", MesocycleId = oldBlock.Id });
        plan.Blocks.Add(new MacrocycleBlock { Id = Guid.NewGuid(), Order = 2, Goal = Goal.Strength, TemplateKey = "x", MesocycleId = newBlock.Id });
        db.Macrocycles.Add(plan);

        await db.SaveChangesAsync();
        return new Seed(oldFirst, oldSecond, newFirst, newSecond, skipped);
    }

    private static Mesocycle NewMesocycle(SqliteDatabase database, string name, DateTime start)
    {
        return new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = name,
            Goal = name == "Blok 2" ? Goal.Strength : Goal.Hypertrophy,
            StartDate = start,
            IsActive = name == "Blok 2"
        };
    }

    private static Guid AddSession(
        Mesocycle mesocycle,
        int weekNumber,
        string dayLabel,
        SessionStatus status,
        DateTime? completedAt,
        DateTime? setAt,
        params Guid[] exerciseIds)
    {
        var week = mesocycle.Weeks.SingleOrDefault(candidate => candidate.WeekNumber == weekNumber);
        if (week is null)
        {
            week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber };
            mesocycle.Weeks.Add(week);
        }

        var session = new WorkoutSession
        {
            Id = Guid.NewGuid(),
            DayLabel = dayLabel,
            Status = status,
            CompletedAt = completedAt
        };

        var order = 0;
        foreach (var exerciseId in exerciseIds)
        {
            order++;
            var plan = new ExercisePlan
            {
                Id = Guid.NewGuid(),
                ExerciseId = exerciseId,
                Order = order,
                TargetSets = 2,
                PrescribedSets = 2,
                RepRangeMin = 3,
                RepRangeMax = 6,
                BaseRepRangeMin = 3,
                BaseRepRangeMax = 6,
                TargetRir = 2
            };

            var isPullUp = order == 2;
            var sets = isPullUp ? 1 : 2;
            for (var number = 1; number <= sets; number++)
            {
                plan.SetLogs.Add(new SetLog
                {
                    Id = Guid.NewGuid(),
                    SetNumber = number,
                    WeightKg = isPullUp ? 5m : 100m,
                    Reps = isPullUp ? 6 : 5,
                    Rir = 2,
                    BodyweightLoadKg = isPullUp ? 80m : 0m,
                    PerformedAt = setAt ?? DateTime.UtcNow
                });
            }

            session.ExercisePlans.Add(plan);
        }

        week.Sessions.Add(session);
        return session.Id;
    }
}
