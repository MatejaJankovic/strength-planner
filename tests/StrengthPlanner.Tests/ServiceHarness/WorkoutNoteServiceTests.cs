using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Notes;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// The lifter's notes: one per workout, and one per exercise that is shown every time the exercise
/// comes up. A note is not training data, so it is allowed in every status of a workout and must
/// change nothing else; and it belongs to one lifter, so another lifter must neither see nor
/// change it.
/// </summary>
public class WorkoutNoteServiceTests
{
    [Fact]
    public async Task AnExerciseNote_IsStoredTrimmed_ReplacedAndCleared()
    {
        using var database = new SqliteDatabase();
        var exercise = database.AddExercise("Seated Row", ExerciseType.Compound, "Machine");

        var created = await SetExerciseNoteAsync(database, exercise, "  Sedište 4, pin 7  ");
        Assert.Equal("Sedište 4, pin 7", created.Note);
        Assert.Equal("Sedište 4, pin 7", (await StoredExerciseNotesAsync(database)).Single().Text);

        await SetExerciseNoteAsync(database, exercise, "Sedište 5");
        var replaced = await StoredExerciseNotesAsync(database);
        Assert.Equal("Sedište 5", replaced.Single().Text);

        var cleared = await SetExerciseNoteAsync(database, exercise, "   ");
        Assert.Null(cleared.Note);
        Assert.Empty(await StoredExerciseNotesAsync(database));
    }

    [Fact]
    public async Task ClearingANoteThatDoesNotExist_IsHarmless()
    {
        using var database = new SqliteDatabase();
        var exercise = database.AddExercise("Seated Row");

        var result = await SetExerciseNoteAsync(database, exercise, null);

        Assert.Null(result.Note);
        Assert.Empty(await StoredExerciseNotesAsync(database));
    }

    [Fact]
    public async Task ANoteLongerThanTheLimit_IsRefused_AndOneAtTheLimitIsKept()
    {
        using var database = new SqliteDatabase();
        var exercise = database.AddExercise("Seated Row");

        var tooLong = new string('x', NotePolicy.MaximumLength + 1);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() => SetExerciseNoteAsync(database, exercise, tooLong));
        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
        Assert.Empty(await StoredExerciseNotesAsync(database));

        var atLimit = new string('x', NotePolicy.MaximumLength);
        Assert.Equal(atLimit, (await SetExerciseNoteAsync(database, exercise, atLimit)).Note);
    }

    /// <summary>
    /// Trimming comes first: a note that is only too long because of its spaces is not too long.
    /// </summary>
    [Fact]
    public async Task TheLimit_IsMeasuredAfterTrimming()
    {
        using var database = new SqliteDatabase();
        var exercise = database.AddExercise("Seated Row");

        var padded = "  " + new string('x', NotePolicy.MaximumLength) + "  ";

        Assert.Equal(NotePolicy.MaximumLength, (await SetExerciseNoteAsync(database, exercise, padded)).Note!.Length);
    }

    [Fact]
    public async Task AnExerciseThatDoesNotExist_IsNotFound()
    {
        using var database = new SqliteDatabase();

        var error = await Assert.ThrowsAsync<TrainingLogException>(
            () => SetExerciseNoteAsync(database, Guid.NewGuid(), "x"));

        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);
    }

    /// <summary>
    /// Another lifter's own exercise is hidden by the ownership filter, so a note on it is a 404, not
    /// a note attached to something the lifter cannot see.
    /// </summary>
    [Fact]
    public async Task AnotherLiftersCustomExercise_IsNotFound()
    {
        using var database = new SqliteDatabase();
        var other = database.AddUser();
        var theirs = Guid.NewGuid();
        await using (var db = database.NewContextFor(other))
        {
            db.Exercises.Add(new Exercise
            {
                Id = theirs,
                Name = "Their Own Lift",
                Type = ExerciseType.Compound,
                Equipment = "Barbell",
                IsCustom = true,
                CreatedByUserId = other
            });
            await db.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<TrainingLogException>(() => SetExerciseNoteAsync(database, theirs, "x"));

        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);
    }

    [Fact]
    public async Task TwoLifters_KeepSeparateNotes_OnTheSameSystemExercise()
    {
        using var database = new SqliteDatabase();
        var exercise = database.AddExercise("Seated Row");
        var other = database.AddUser();

        await SetExerciseNoteAsync(database, exercise, "moja");
        await using (var db = database.NewContextFor(other))
        {
            await new WorkoutNoteService(db).SetExerciseNoteAsync(other, exercise, "njegova", CancellationToken.None);
        }

        // Each lifter reads only their own row...
        await using (var mine = database.NewContext())
        {
            var visible = await ExerciseNoteResolver.ResolveAsync(mine, database.UserId, [exercise], CancellationToken.None);
            Assert.Equal("moja", visible[exercise]);
        }

        await using (var theirs = database.NewContextFor(other))
        {
            var visible = await ExerciseNoteResolver.ResolveAsync(theirs, other, [exercise], CancellationToken.None);
            Assert.Equal("njegova", visible[exercise]);
        }

        // ...and clearing one leaves the other.
        await SetExerciseNoteAsync(database, exercise, null);
        await using var after = database.NewContextFor(other);
        var left = await ExerciseNoteResolver.ResolveAsync(after, other, [exercise], CancellationToken.None);
        Assert.Equal("njegova", left[exercise]);
    }

    /// <summary>
    /// A session note can be written in every status, and writing one changes nothing about the workout:
    /// not its status, not the completion time, not the flag that says a correction is waiting to be saved.
    /// </summary>
    [Theory]
    [InlineData(SessionStatus.Planned)]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Skipped)]
    public async Task ASessionNote_CanBeWritten_InEveryStatus_AndChangesNothingElse(SessionStatus status)
    {
        using var database = new SqliteDatabase();
        var completedAt = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);
        var sessionId = await SeedSessionAsync(database, database.UserId, status, completedAt);

        await using var db = database.NewContext();
        var result = await new WorkoutNoteService(db).SetSessionNoteAsync(
            database.UserId, sessionId, "  Spavao 5h, koleno  ", CancellationToken.None);
        Assert.Equal("Spavao 5h, koleno", result.Note);

        await using var read = database.NewContext();
        var stored = await read.WorkoutSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId);
        Assert.Equal("Spavao 5h, koleno", stored.Note);
        Assert.Equal(status, stored.Status);
        Assert.Equal(completedAt, stored.CompletedAt);
        Assert.False(stored.RevisionPending);
    }

    [Fact]
    public async Task ASessionNote_CanBeCleared_AndIsRefusedWhenTooLong()
    {
        using var database = new SqliteDatabase();
        var sessionId = await SeedSessionAsync(database, database.UserId, SessionStatus.Completed, null);

        await using var db = database.NewContext();
        var service = new WorkoutNoteService(db);
        await service.SetSessionNoteAsync(database.UserId, sessionId, "jedan", CancellationToken.None);

        var cleared = await service.SetSessionNoteAsync(database.UserId, sessionId, " ", CancellationToken.None);
        Assert.Null(cleared.Note);

        var error = await Assert.ThrowsAsync<TrainingLogException>(() => service.SetSessionNoteAsync(
            database.UserId, sessionId, new string('x', NotePolicy.MaximumLength + 1), CancellationToken.None));
        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);

        await using var read = database.NewContext();
        Assert.Null((await read.WorkoutSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId)).Note);
    }

    [Fact]
    public async Task ASessionOfAnotherLifter_IsNotFound_AndItsNoteIsLeftAlone()
    {
        using var database = new SqliteDatabase();
        var other = database.AddUser();
        var theirs = await SeedSessionAsync(database, other, SessionStatus.Planned, null);

        await using var db = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            new WorkoutNoteService(db).SetSessionNoteAsync(database.UserId, theirs, "upadam", CancellationToken.None));
        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);

        await using var read = database.NewContextFor(other);
        Assert.Null((await read.WorkoutSessions.AsNoTracking().SingleAsync(session => session.Id == theirs)).Note);
    }

    [Fact]
    public async Task ASessionThatDoesNotExist_IsNotFound()
    {
        using var database = new SqliteDatabase();

        await using var db = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            new WorkoutNoteService(db).SetSessionNoteAsync(database.UserId, Guid.NewGuid(), "x", CancellationToken.None));

        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);
    }

    /// <summary>
    /// The workout screen gets both notes in the one session it already reads, and the exercise note
    /// comes with the exercise: it is there in a different workout of the same exercise, and absent
    /// from an exercise nobody wrote about.
    /// </summary>
    [Fact]
    public async Task TheWorkoutScreen_ReceivesTheSessionNote_AndTheExerciseNote_InEveryWorkoutOfThatExercise()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedTwoWorkoutsAsync(database);

        await using (var db = database.NewContext())
        {
            var notes = new WorkoutNoteService(db);
            await notes.SetExerciseNoteAsync(database.UserId, seed.Row, "Sedište 4, pin 7", CancellationToken.None);
            await notes.SetSessionNoteAsync(database.UserId, seed.First, "Težak dan", CancellationToken.None);
        }

        var first = await GetSessionAsync(database, seed.First);
        var second = await GetSessionAsync(database, seed.Second);

        Assert.Equal("Težak dan", first.Note);
        Assert.Null(second.Note);
        Assert.Equal([null, "Sedište 4, pin 7"], first.ExercisePlans.Select(plan => plan.ExerciseNote));
        Assert.Equal([null, "Sedište 4, pin 7"], second.ExercisePlans.Select(plan => plan.ExerciseNote));
    }

    [Fact]
    public async Task TheHistory_CarriesTheNoteOfAWorkout()
    {
        using var database = new SqliteDatabase();
        var sessionId = await SeedSessionAsync(
            database, database.UserId, SessionStatus.Completed, new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc));

        await using (var db = database.NewContext())
        {
            await new WorkoutNoteService(db).SetSessionNoteAsync(database.UserId, sessionId, "Dobar", CancellationToken.None);
        }

        await using var read = database.NewContext();
        var history = await new TrainingHistoryService(read).GetAsync(database.UserId, 1, 20);

        Assert.Equal("Dobar", history.Items.Single().Note);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\n\t ", null)]
    [InlineData("  a b  ", "a b")]
    [InlineData("jedan\ndva", "jedan\ndva")]
    public void Normalize_TrimsTheEnds_AndTurnsNothingIntoNull(string? input, string? expected)
    {
        Assert.Equal(expected, NotePolicy.Normalize(input));
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static async Task<StrengthPlanner.Application.DTOs.Notes.NoteDto> SetExerciseNoteAsync(
        SqliteDatabase database,
        Guid exerciseId,
        string? note)
    {
        await using var db = database.NewContext();
        return await new WorkoutNoteService(db).SetExerciseNoteAsync(database.UserId, exerciseId, note, CancellationToken.None);
    }

    private static async Task<List<UserExerciseNote>> StoredExerciseNotesAsync(SqliteDatabase database)
    {
        await using var db = database.NewContext();
        return await db.UserExerciseNotes.AsNoTracking().ToListAsync();
    }

    private static async Task<Guid> SeedSessionAsync(
        SqliteDatabase database,
        Guid owner,
        SessionStatus status,
        DateTime? completedAt)
    {
        var sessionId = Guid.NewGuid();
        await using var db = database.NewContextFor(owner);
        db.Mesocycles.Add(new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = owner,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            Weeks =
            [
                new TrainingWeek
                {
                    Id = Guid.NewGuid(),
                    WeekNumber = 1,
                    Sessions =
                    [
                        new WorkoutSession
                        {
                            Id = sessionId,
                            DayLabel = "Day A",
                            Status = status,
                            CompletedAt = completedAt
                        }
                    ]
                }
            ]
        });
        await db.SaveChangesAsync();
        return sessionId;
    }

    private sealed record TwoWorkouts(Guid First, Guid Second, Guid Row);

    private static async Task<TwoWorkouts> SeedTwoWorkoutsAsync(SqliteDatabase database)
    {
        var press = database.AddExercise("Chest Press", ExerciseType.Compound, "Machine");
        var row = database.AddExercise("Seated Row", ExerciseType.Compound, "Machine");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await using var db = database.NewContext();

        WorkoutSession Session(Guid id, string label) => new()
        {
            Id = id,
            DayLabel = label,
            Status = SessionStatus.Planned,
            ExercisePlans =
            [
                Plan(press, order: 1),
                Plan(row, order: 2)
            ]
        };

        // Both workouts hold the press (order 1) and the row (order 2). The note is written for the
        // row only, so it must show on the row in both workouts and never on the press.
        db.Mesocycles.Add(new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            Weeks =
            [
                new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 1, Sessions = [Session(first, "Day A")] },
                new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = 2, Sessions = [Session(second, "Day A")] }
            ]
        });
        await db.SaveChangesAsync();

        return new TwoWorkouts(first, second, row);
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

    private static async Task<StrengthPlanner.Application.DTOs.Mesocycles.WorkoutSessionDto> GetSessionAsync(
        SqliteDatabase database,
        Guid sessionId)
    {
        await using var context = database.NewContext();
        var landmarks = new VolumeLandmarkService(context);
        var service = new SessionService(
            context,
            landmarks,
            new DeloadService(context, landmarks),
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)),
            null!);

        return await service.GetByIdAsync(database.UserId, sessionId, CancellationToken.None);
    }
}
