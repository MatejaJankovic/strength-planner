using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// A finished workout can be corrected while nothing has been built on it, and saving the
/// correction writes again everything that finishing it wrote: the estimate, the target of the
/// same day next week, the week's fatigue score and its move of the volume limits.
///
/// The workouts here are finished by the real <c>CompleteAsync</c>, so the correction is
/// measured against what completion really produced and not against a hand-built row.
/// </summary>
public class SessionRevisionTests
{
    [Fact]
    public async Task ASavedCorrection_ReplacesTheEstimate_AndTheNextTarget()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        var first = await CompleteAsync(database, seed.SessionOne);
        var firstSquat = first.Exercises.Single();
        Assert.Equal(102.5m, firstSquat.NextWeightKg);

        // The lifter entered 12 reps three times, and it was 8: the estimate and the step up
        // were both built on sets that were not lifted.
        await SetRepsAsync(database, seed.SessionOne, reps: 8);
        var result = await ReviseAsync(database, seed.SessionOne);

        var squat = result.Result.Exercises.Single();
        Assert.True(squat.E1Rm < firstSquat.E1Rm);
        Assert.Equal(100m, squat.NextWeightKg);
        Assert.Equal(100m, await NextTargetAsync(database, seed));

        await using var read = database.NewContext();
        var estimates = await read.OneRepMaxRecords
            .Where(record => record.Source == OneRepMaxSource.Estimated)
            .ToListAsync();
        Assert.Equal(squat.E1Rm, Assert.Single(estimates).ValueKg);
        Assert.Equal(seed.SessionOne, estimates[0].WorkoutSessionId);
        Assert.False((await read.WorkoutSessions.SingleAsync(session => session.Id == seed.SessionOne)).RevisionPending);
    }

    /// <summary>
    /// Saving with nothing changed writes the same values again, so a correction that was
    /// interrupted and repeated cannot drift.
    /// </summary>
    [Fact]
    public async Task SavingWithNothingChanged_WritesTheSameValues()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        var first = await CompleteAsync(database, seed.SessionOne);

        var again = await ReviseAsync(database, seed.SessionOne);

        Assert.Equal(first.Exercises.Single().E1Rm, again.Result.Exercises.Single().E1Rm);
        Assert.Equal(first.Exercises.Single().NextWeightKg, again.Result.Exercises.Single().NextWeightKg);
        Assert.Equal(again.PreviousFatigueScore, again.FatigueScore);
        await using var read = database.NewContext();
        Assert.Equal(1, await read.OneRepMaxRecords.CountAsync(record => record.Source == OneRepMaxSource.Estimated));
    }

    /// <summary>
    /// The week was closed when the workout was finished, so its fatigue score is worked out
    /// again from the corrected sets and the report says what it was and what it is.
    /// </summary>
    [Fact]
    public async Task ACorrectionOfAClosedWeek_ScoresTheWeekAgain()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 4, reps: 10, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        await using (var db = database.NewContext())
        {
            var before = (await db.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekOne)).FatigueScore;
            Assert.NotNull(before);
        }

        // Every set taken to failure instead of one rep from it.
        await SetEverySetAsync(database, seed.SessionOne, reps: 10, rir: 0, isFailure: true);
        var result = await ReviseAsync(database, seed.SessionOne);

        Assert.NotNull(result.PreviousFatigueScore);
        Assert.NotEqual(result.PreviousFatigueScore, result.FatigueScore);
        await using var read = database.NewContext();
        Assert.Equal(
            result.FatigueScore,
            (await read.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekOne)).FatigueScore);
    }

    /// <summary>
    /// Twenty hard sets at the ceiling of the volume score high enough to pull
    /// the next week forward as a deload. A week that was entered easy and corrected to that is
    /// treated as if it had been entered right: the deload comes, and the report says it did.
    /// </summary>
    [Fact]
    public async Task ACorrectionThatRaisesTheScoreOverTheThreshold_PullsTheDeloadForward()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 20, reps: 10, rir: 3);
        var first = await CompleteAsync(database, seed.SessionOne);
        Assert.Null(first.AutoDeload);

        await SetEverySetAsync(database, seed.SessionOne, reps: 0, rir: 0, isFailure: false, hard: true);
        var result = await ReviseAsync(database, seed.SessionOne);

        Assert.NotNull(result.Result.AutoDeload);
        Assert.Equal(2, result.Result.AutoDeload.DeloadWeek);
        Assert.False(result.DeloadKept);
        await using var read = database.NewContext();
        Assert.True((await read.TrainingWeeks.SingleAsync(week => week.MesocycleId == seed.MesocycleId && week.WeekNumber == 2)).IsAutoDeload);
    }

    /// <summary>
    /// The reverse: the sets were entered hard and pulled a deload forward, then corrected to
    /// easy ones. The next week was rewritten for the deload and the lifter may have seen it, so
    /// it stays, and the report says so instead of quietly undoing it.
    /// </summary>
    [Fact]
    public async Task ADeloadThatWasPulledForward_StaysWhenTheCorrectedScoreWouldNotHaveCalledForIt()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 20, reps: 0, rir: 0, hard: true);
        var first = await CompleteAsync(database, seed.SessionOne);
        Assert.NotNull(first.AutoDeload);

        await SetEverySetAsync(database, seed.SessionOne, reps: 10, rir: 3, isFailure: false);
        var result = await ReviseAsync(database, seed.SessionOne);

        Assert.True(result.DeloadKept);
        Assert.Null(result.Result.AutoDeload);
        Assert.True(result.FatigueScore < result.PreviousFatigueScore);
        await using var read = database.NewContext();
        Assert.True((await read.TrainingWeeks.SingleAsync(week => week.MesocycleId == seed.MesocycleId && week.WeekNumber == 2)).IsAutoDeload);
    }

    /// <summary>
    /// The limits are not recalculated on top of the old move: that would count the week
    /// twice. Twenty easy sets raise the ceiling by one (20 to 21); corrected to twenty hard
    /// ones the same week lowers it (to 19), and not to 20, which is what counting it twice
    /// would give.
    /// </summary>
    [Fact]
    public async Task ACorrectionOfAClosedWeek_TakesBackItsMoveOfTheLimits_BeforeCountingItAgain()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 20, reps: 10, rir: 3);
        await CompleteAsync(database, seed.SessionOne);

        Assert.Equal(21, await MrvAsync(database, seed.Quads));
        await using (var db = database.NewContext())
        {
            var change = await db.VolumeLandmarkChanges.SingleAsync();
            Assert.Equal((0, 0, 1), (change.MevDelta, change.MavDelta, change.MrvDelta));
        }

        await SetEverySetAsync(database, seed.SessionOne, reps: 10, rir: 0, isFailure: true);
        await ReviseAsync(database, seed.SessionOne);

        Assert.Equal(19, await MrvAsync(database, seed.Quads));
        await using var read = database.NewContext();
        var changes = await read.VolumeLandmarkChanges.ToListAsync();
        Assert.Equal(-1, Assert.Single(changes).MrvDelta);
    }

    /// <summary>
    /// A week that moved the limits and is corrected back to where it started leaves them at
    /// the seed, with no personal row that only repeats it.
    /// </summary>
    [Fact]
    public async Task UndoingTheOnlyMove_LeavesNoPersonalRow()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 20, reps: 10, rir: 3);
        await CompleteAsync(database, seed.SessionOne);
        Assert.Equal(21, await MrvAsync(database, seed.Quads));

        await using (var context = database.NewContext())
        {
            var landmarks = new VolumeLandmarkService(context);
            await landmarks.UndoWeekAsync(database.UserId, seed.MesocycleId, seed.WeekOne, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        Assert.Empty(await read.UserVolumeLandmarks.ToListAsync());
        Assert.Empty(await read.VolumeLandmarkChanges.ToListAsync());
        Assert.Null((await read.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekOne)).VolumeAdaptedAt);
    }

    [Fact]
    public async Task TheSessionDto_SaysWhetherItCanBeCorrected_AndWhetherASaveIsPending()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        var before = await ReadSessionAsync(database, seed.SessionOne);
        Assert.NotNull(before.Correction);
        Assert.True(before.Correction.CanCorrect);
        Assert.Equal("Recalculate", before.Correction.Mode);
        Assert.False(before.Correction.RevisionPending);

        await SetRepsAsync(database, seed.SessionOne, reps: 9);

        var pending = await ReadSessionAsync(database, seed.SessionOne);
        Assert.True(pending.Correction!.RevisionPending);

        await ReviseAsync(database, seed.SessionOne);
        Assert.False((await ReadSessionAsync(database, seed.SessionOne)).Correction!.RevisionPending);

        // A session that was not finished has no correction at all.
        var planned = await ReadSessionAsync(database, seed.SessionTwo);
        Assert.Null(planned.Correction);
    }

    /// <summary>
    /// Once the same day of the next week is under way its target was built from these sets
    /// and they are final: changing a set is refused, and so is saving.
    /// </summary>
    [Fact]
    public async Task TheWindowCloses_WhenTheNextWorkoutOfTheSameDayStarts()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        await using (var db = database.NewContext())
        {
            (await db.WorkoutSessions.SingleAsync(session => session.Id == seed.SessionTwo)).Status = SessionStatus.InProgress;
            await db.SaveChangesAsync();
        }

        var setId = await FirstSetIdAsync(database, seed.SessionOne);
        await using var context = database.NewContext();
        var update = await Assert.ThrowsAsync<TrainingLogException>(() =>
            new SetLogService(context).UpdateSetAsync(
                database.UserId,
                setId,
                new UpdateSetLogRequest { WeightKg = 100m, Reps = 8, Rir = 1 }));
        Assert.Equal(TrainingLogErrorType.Conflict, update.ErrorType);

        await using var other = database.NewContext();
        var revise = await Assert.ThrowsAsync<TrainingLogException>(() => NewRevisionService(other).ReviseAsync(database.UserId, seed.SessionOne));
        Assert.Equal(TrainingLogErrorType.Conflict, revise.ErrorType);

        var dto = await ReadSessionAsync(database, seed.SessionOne);
        Assert.False(dto.Correction!.CanCorrect);
        Assert.Equal("NextWorkoutStarted", dto.Correction.BlockedBy);
    }

    /// <summary>
    /// A workout finished before consequences were recorded per workout has no estimate or
    /// week move tied to it. Its sets can still be corrected, as history, and nothing is
    /// recalculated: no pending save, no new estimate, the next target stays.
    /// </summary>
    [Fact]
    public async Task AWorkoutFinishedBeforeConsequencesWereRecorded_IsCorrectedAsHistoryOnly()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        await using (var db = database.NewContext())
        {
            var session = await db.WorkoutSessions.SingleAsync(candidate => candidate.Id == seed.SessionOne);
            session.CompletedAt = null;
            await db.OneRepMaxRecords
                .Where(record => record.WorkoutSessionId == seed.SessionOne)
                .ExecuteUpdateAsync(setters => setters.SetProperty(record => record.WorkoutSessionId, (Guid?)null));
            await db.SaveChangesAsync();
        }

        var targetBefore = await NextTargetAsync(database, seed);
        await SetRepsAsync(database, seed.SessionOne, reps: 8);

        var dto = await ReadSessionAsync(database, seed.SessionOne);
        Assert.Equal("HistoryOnly", dto.Correction!.Mode);
        Assert.False(dto.Correction.RevisionPending);
        Assert.Equal(targetBefore, await NextTargetAsync(database, seed));

        await using var read = database.NewContext();
        Assert.All(
            (await read.SetLogs.Where(set => set.ExercisePlan.WorkoutSessionId == seed.SessionOne).ToListAsync()),
            set => Assert.Equal(8, set.Reps));
        Assert.Equal(1, await read.OneRepMaxRecords.CountAsync(record => record.Source == OneRepMaxSource.Estimated));

        await using var context = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() => NewRevisionService(context).ReviseAsync(database.UserId, seed.SessionOne));
        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    [Fact]
    public async Task AddingASet_ToAFinishedWorkout_MarksTheCorrectionPending()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        await using (var context = database.NewContext())
        {
            await new SetLogService(context).AddSetAsync(
                database.UserId,
                seed.SquatPlanOne,
                new AddSetLogRequest { WeightKg = 100m, Reps = 10, Rir = 1 });
        }

        Assert.True((await ReadSessionAsync(database, seed.SessionOne)).Correction!.RevisionPending);
    }

    [Fact]
    public async Task AnotherUser_CannotReviseOrCorrect()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);
        var setId = await FirstSetIdAsync(database, seed.SessionOne);

        await using var context = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(
            () => NewRevisionService(context).ReviseAsync(Guid.NewGuid(), seed.SessionOne));
        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);

        await using var other = database.NewContext();
        var update = await Assert.ThrowsAsync<TrainingLogException>(
            () => new SetLogService(other).UpdateSetAsync(
                Guid.NewGuid(),
                setId,
                new UpdateSetLogRequest { WeightKg = 100m, Reps = 8, Rir = 1 }));
        Assert.Equal(TrainingLogErrorType.NotFound, update.ErrorType);

        await using var read = database.NewContext();
        Assert.All(
            await read.SetLogs.Where(set => set.ExercisePlan.WorkoutSessionId == seed.SessionOne).ToListAsync(),
            set => Assert.Equal(12, set.Reps));
    }

    /// <summary>
    /// A skipped same-day workout in the next week settles that week, and in the last week of a
    /// block it generates the next block from the estimates. Treating it as not started left the
    /// window open after both, so the correction would have recalculated what was already used.
    /// </summary>
    [Fact]
    public async Task ASkippedNextWorkout_ClosesTheWindow_ThroughTheServices()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);

        await using (var db = database.NewContext())
        {
            (await db.WorkoutSessions.SingleAsync(session => session.Id == seed.SessionTwo)).Status = SessionStatus.Skipped;
            await db.SaveChangesAsync();
        }

        var dto = await ReadSessionAsync(database, seed.SessionOne);
        Assert.False(dto.Correction!.CanCorrect);
        Assert.Equal("NextWorkoutStarted", dto.Correction.BlockedBy);

        var setId = await FirstSetIdAsync(database, seed.SessionOne);
        await using var context = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            new SetLogService(context).UpdateSetAsync(
                database.UserId,
                setId,
                new UpdateSetLogRequest { WeightKg = 100m, Reps = 8, Rir = 1 }));
        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    /// <summary>
    /// The last workout of a block has no later week to hand a target to; what its sets fed is
    /// the next block. While that block has not been generated the window is open, and once it
    /// has, it is closed. This goes through the database; the domain test only covers the rule.
    /// </summary>
    [Fact]
    public async Task InTheLastWeek_TheWindowClosesWhenTheNextBlockIsGenerated()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        var blockTwoId = Guid.NewGuid();

        // Week 2's workout is the last one of this block: weeks 3 and 4 have no session.
        await using (var db = database.NewContext())
        {
            var last = await db.WorkoutSessions.SingleAsync(session => session.Id == seed.SessionTwo);
            last.Status = SessionStatus.Completed;
            last.CompletedAt = DateTime.UtcNow;

            var plan = new Macrocycle { Id = Guid.NewGuid(), UserId = database.UserId, Name = "Plan", StartDate = DateTime.UtcNow, IsActive = true };
            plan.Blocks.Add(new MacrocycleBlock { Id = Guid.NewGuid(), Order = 1, Goal = Goal.Hypertrophy, TemplateKey = "x", MesocycleId = seed.MesocycleId });
            plan.Blocks.Add(new MacrocycleBlock { Id = blockTwoId, Order = 2, Goal = Goal.Hypertrophy, TemplateKey = "x" });
            db.Macrocycles.Add(plan);
            await db.SaveChangesAsync();
        }

        Assert.True((await ReadSessionAsync(database, seed.SessionTwo)).Correction!.CanCorrect);

        var secondBlockMesocycle = Guid.NewGuid();
        await using (var db = database.NewContext())
        {
            db.Mesocycles.Add(new Mesocycle
            {
                Id = secondBlockMesocycle,
                UserId = database.UserId,
                Name = "Blok 2",
                Goal = Goal.Hypertrophy,
                StartDate = DateTime.UtcNow,
                IsActive = false
            });
            await db.SaveChangesAsync();
            (await db.MacrocycleBlocks.SingleAsync(block => block.Id == blockTwoId)).MesocycleId = secondBlockMesocycle;
            await db.SaveChangesAsync();
        }

        var closed = await ReadSessionAsync(database, seed.SessionTwo);
        Assert.False(closed.Correction!.CanCorrect);
        Assert.Equal("NextBlockOpened", closed.Correction.BlockedBy);
    }

    /// <summary>
    /// A correction the lifter left unsaved is saved before the next workout of the same day
    /// starts. Starting it closes the window, and without this the sets would be changed and
    /// nothing built from them would be, with no later request able to save the correction.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUnsavedCorrection_IsAppliedBeforeTheNextWorkoutIsStartedOrSkipped(bool skip)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await CompleteAsync(database, seed.SessionOne);
        Assert.Equal(102.5m, await NextTargetAsync(database, seed));

        await SetRepsAsync(database, seed.SessionOne, reps: 8);
        Assert.True((await ReadSessionAsync(database, seed.SessionOne)).Correction!.RevisionPending);

        await using (var context = database.NewContext())
        {
            var service = NewSessionServiceWithRevisions(context);
            if (skip)
            {
                await service.SkipAsync(database.UserId, seed.SessionTwo);
            }
            else
            {
                await service.StartAsync(database.UserId, seed.SessionTwo);
            }
        }

        // The correction was saved first: the target was recalculated from the eight reps, and
        // the flag is gone, so the window closing after it loses nothing.
        Assert.Equal(100m, await NextTargetAsync(database, seed));
        await using var read = database.NewContext();
        Assert.False((await read.WorkoutSessions.SingleAsync(session => session.Id == seed.SessionOne)).RevisionPending);
        var estimate = Assert.Single(await read.OneRepMaxRecords.Where(record => record.Source == OneRepMaxSource.Estimated).ToListAsync());
        Assert.Equal(seed.SessionOne, estimate.WorkoutSessionId);
    }

    /// <summary>
    /// The week still waits for another workout, so it was never scored and never counted by the
    /// limits: there is nothing of the week to take back, and the correction must not score it
    /// early either. Every other test has one workout a week, so the week is always closed.
    /// </summary>
    [Fact]
    public async Task ACorrectionInAnOpenWeek_DoesNotScoreOrCountTheWeek()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, sets: 3, reps: 12, rir: 1);
        await using (var db = database.NewContext())
        {
            var week = await db.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekOne);
            db.WorkoutSessions.Add(new WorkoutSession
            {
                Id = Guid.NewGuid(),
                TrainingWeekId = week.Id,
                DayLabel = "Day B",
                Status = SessionStatus.Planned
            });
            await db.SaveChangesAsync();
        }

        await CompleteAsync(database, seed.SessionOne);
        await using (var db = database.NewContext())
        {
            var week = await db.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekOne);
            Assert.Null(week.FatigueScore);
            Assert.Null(week.VolumeAdaptedAt);
        }

        await SetRepsAsync(database, seed.SessionOne, reps: 8);
        var result = await ReviseAsync(database, seed.SessionOne);

        Assert.Null(result.PreviousFatigueScore);
        Assert.Null(result.FatigueScore);
        Assert.Equal(100m, await NextTargetAsync(database, seed));
        await using var read = database.NewContext();
        Assert.Null((await read.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekOne)).FatigueScore);
        Assert.Empty(await read.VolumeLandmarkChanges.ToListAsync());
    }

    // ---- helpers ------------------------------------------------------------------------

    private static SessionRevisionService NewRevisionService(AppDbContext context)
    {
        var landmarks = new VolumeLandmarkService(context);
        return new SessionRevisionService(
            context,
            landmarks,
            new DeloadService(context, landmarks),
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)));
    }

    private static StrengthPlanner.Infrastructure.TrainingLogs.SessionService NewSessionServiceWithRevisions(AppDbContext context)
    {
        var landmarks = new VolumeLandmarkService(context);
        var deloads = new DeloadService(context, landmarks);
        var planner = new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context));
        return new StrengthPlanner.Infrastructure.TrainingLogs.SessionService(
            context,
            landmarks,
            deloads,
            planner,
            new MacrocycleService(context, null!, null!),
            new SessionRevisionService(context, landmarks, deloads, planner));
    }

    private static async Task<StrengthPlanner.Application.DTOs.Sessions.CompleteSessionResultDto> CompleteAsync(
        SqliteDatabase database,
        Guid sessionId)
    {
        await using var context = database.NewContext();
        return await SessionCompletionTests.NewService(context).CompleteAsync(database.UserId, sessionId, CancellationToken.None);
    }

    private static async Task<StrengthPlanner.Application.DTOs.Sessions.SessionRevisionResultDto> ReviseAsync(
        SqliteDatabase database,
        Guid sessionId)
    {
        await using var context = database.NewContext();
        return await NewRevisionService(context).ReviseAsync(database.UserId, sessionId, CancellationToken.None);
    }

    private static async Task<StrengthPlanner.Application.DTOs.Mesocycles.WorkoutSessionDto> ReadSessionAsync(
        SqliteDatabase database,
        Guid sessionId)
    {
        await using var context = database.NewContext();
        return await SessionCompletionTests.NewService(context).GetByIdAsync(database.UserId, sessionId, CancellationToken.None);
    }

    private static async Task<Guid> FirstSetIdAsync(SqliteDatabase database, Guid sessionId)
    {
        await using var db = database.NewContext();
        return await db.SetLogs
            .Where(set => set.ExercisePlan.WorkoutSessionId == sessionId)
            .OrderBy(set => set.SetNumber)
            .Select(set => set.Id)
            .FirstAsync();
    }

    /// <summary>
    /// A hard week, with two real signals so it can pull a deload forward by itself: half the
    /// sets taken to failure, the rest six reps (two under the range of eight) with one in
    /// reserve, and at twenty sets the volume sits on the ceiling. Failures are left out of the
    /// reserve signal, so it takes the second half to carry that one. The sets are not zero
    /// in reserve, because the service reads such a set as a failure and the halves would blur.
    /// </summary>
    private static (int Reps, int Rir, bool IsFailure) HardSet(int number)
    {
        return number % 2 == 0 ? (8, 0, true) : (6, 1, false);
    }

    /// <summary>Changes every logged set of the session through the same service the screen uses.</summary>
    private static async Task SetRepsAsync(SqliteDatabase database, Guid sessionId, int reps)
    {
        await SetEverySetAsync(database, sessionId, reps, rir: 1, isFailure: false);
    }

    private static async Task SetEverySetAsync(
        SqliteDatabase database,
        Guid sessionId,
        int reps,
        int rir,
        bool isFailure,
        bool hard = false)
    {
        List<Guid> setIds;
        await using (var db = database.NewContext())
        {
            setIds = await db.SetLogs
                .Where(set => set.ExercisePlan.WorkoutSessionId == sessionId)
                .OrderBy(set => set.SetNumber)
                .Select(set => set.Id)
                .ToListAsync();
        }

        var number = 0;
        foreach (var setId in setIds)
        {
            number++;
            var (setReps, setRir, failure) = hard ? HardSet(number) : (reps, rir, isFailure);
            await using var context = database.NewContext();
            await new SetLogService(context).UpdateSetAsync(
                database.UserId,
                setId,
                new UpdateSetLogRequest { WeightKg = 100m, Reps = setReps, Rir = setRir, IsFailure = failure });
        }
    }

    private static async Task<decimal?> NextTargetAsync(SqliteDatabase database, Seed seed)
    {
        await using var db = database.NewContext();
        return (await db.ExercisePlans.SingleAsync(plan => plan.WorkoutSessionId == seed.SessionTwo)).TargetWeightKg;
    }

    private static async Task<int> MrvAsync(SqliteDatabase database, Guid muscleGroupId)
    {
        await using var db = database.NewContext();
        var personal = await db.UserVolumeLandmarks.SingleOrDefaultAsync(landmark => landmark.MuscleGroupId == muscleGroupId);
        if (personal is not null)
        {
            return personal.Mrv;
        }

        return (await db.VolumeLandmarks.SingleAsync(landmark => landmark.MuscleGroupId == muscleGroupId)).Mrv;
    }

    private sealed record Seed(
        Guid Quads,
        Guid MesocycleId,
        Guid WeekOne,
        Guid SessionOne,
        Guid SessionTwo,
        Guid SquatPlanOne);

    /// <summary>
    /// A hypertrophy block of four weeks with one day ("Day A") a week and one lift, a squat
    /// with a typed maximum of 130 kg. The first workout is under way with <paramref name="sets"/>
    /// sets of 100 kg; the second is planned. The block follows its template, so finishing a
    /// workout does not rebalance anything that is not under test here.
    /// </summary>
    private static async Task<Seed> SeedAsync(
        SqliteDatabase database,
        int sets,
        int reps,
        int rir,
        bool hard = false)
    {
        var quads = Guid.NewGuid();
        var squat = Guid.NewGuid();

        await using var db = database.NewContext();
        db.MuscleGroups.Add(new MuscleGroup { Id = quads, Name = "Quads" });
        db.VolumeLandmarks.Add(new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = quads, Mev = 8, Mav = 12, Mrv = 20 });
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Age = 30,
            BodyweightKg = 80m,
            ExperienceLevel = ExperienceLevel.Intermediate
        });
        db.Exercises.Add(new Exercise
        {
            Id = squat,
            Name = "Back Squat",
            Type = ExerciseType.Compound,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            Muscles = [new ExerciseMuscle { MuscleGroupId = quads, Contribution = 1.0m }]
        });
        db.OneRepMaxRecords.Add(new OneRepMaxRecord
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            ExerciseId = squat,
            ValueKg = 130m,
            Source = OneRepMaxSource.Manual,
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        });

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            PeriodizationModel = PeriodizationModel.Flat,
            SetAllocation = SetAllocation.FollowTemplate,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        Guid weekOne = default, sessionOne = default, sessionTwo = default, planOne = default;
        for (var weekNumber = 1; weekNumber <= 4; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 4 };
            mesocycle.Weeks.Add(week);
            if (weekNumber > 2)
            {
                continue;
            }

            var session = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber == 1 ? SessionStatus.InProgress : SessionStatus.Planned
            };
            var plan = new ExercisePlan
            {
                Id = Guid.NewGuid(),
                ExerciseId = squat,
                Order = 1,
                TargetSets = sets,
                PrescribedSets = sets,
                RepRangeMin = 8,
                RepRangeMax = 12,
                BaseRepRangeMin = 8,
                BaseRepRangeMax = 12,
                TargetRir = 1,
                TargetWeightKg = 100m
            };
            if (weekNumber == 1)
            {
                for (var number = 1; number <= sets; number++)
                {
                    var (setReps, setRir, failure) = hard ? HardSet(number) : (reps, rir, false);
                    plan.SetLogs.Add(new SetLog
                    {
                        Id = Guid.NewGuid(),
                        SetNumber = number,
                        WeightKg = 100m,
                        Reps = setReps,
                        Rir = setRir,
                        IsFailure = failure,
                        PerformedAt = DateTime.UtcNow
                    });
                }

                weekOne = week.Id;
                sessionOne = session.Id;
                planOne = plan.Id;
            }
            else
            {
                sessionTwo = session.Id;
            }

            session.ExercisePlans.Add(plan);
            week.Sessions.Add(session);
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new Seed(quads, mesocycle.Id, weekOne, sessionOne, sessionTwo, planOne);
    }
}
