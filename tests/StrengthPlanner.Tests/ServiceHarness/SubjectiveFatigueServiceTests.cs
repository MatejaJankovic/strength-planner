using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Odgovor o umoru (1-5) stiže sa završetkom treninga i ulazi u ocenu umora nedelje. Pravilo je u domenu
/// (<c>SubjectiveFatigue</c>, <c>FatigueEvaluator</c>), ali ga servis hrani: odgovor se mora upisati pre nego što
/// se nedelja ocenjuje, a osnova i potvrda se čitaju iz ranijih nedelja istog bloka i samo njih. Bez ovih testova
/// bi se bilo koja od te tri stvari mogla vratiti uz ceo paket zelen.
/// </summary>
public class SubjectiveFatigueServiceTests
{
    /// <summary>Odgovor trećeg treninga, uz porast od 2 i 3 u ranijim nedeljama, daje celu težinu signala: 0.20.</summary>
    [Fact]
    public async Task ARiseConfirmedByThePreviousWeek_AddsItsWeight_ToTheWeeksScore()
    {
        var withoutAnswers = await ScoreOfWeekThreeAsync(null, null, null);
        var withAnswers = await ScoreOfWeekThreeAsync(2, 3, 4);

        Assert.Equal(withoutAnswers + 0.20m, withAnswers);
    }

    /// <summary>
    /// Porast samo u ovoj nedelji nije potvrđen: slab dan diže odgovor kao što spušta snagu i rezervu, pa jedna
    /// loša nedelja ne sme da se izbroji tri puta.
    /// </summary>
    [Fact]
    public async Task ARiseInOneWeekOnly_IsNotConfirmed_AndChangesNothing()
    {
        var control = await ScoreOfWeekThreeAsync(null, null, null);

        Assert.Equal(control, await ScoreOfWeekThreeAsync(2, 2, 5));
    }

    [Fact]
    public async Task ALifterWhoNeverAnswers_IsScoredAsBefore()
    {
        var score = await ScoreOfWeekThreeAsync(null, null, null);

        Assert.True(score < 0.20m);
    }

    /// <summary>
    /// Odgovor se ne čita po nivou: vežbač koji uvek kaže 4 nema porast, pa ni signal. Ono što nosi podatak je
    /// nedelja koja se oseća gore od njegovih ranijih nedelja.
    /// </summary>
    [Fact]
    public async Task ALifterWhoAlwaysAnswersFour_HasNoRise()
    {
        var control = await ScoreOfWeekThreeAsync(null, null, null);

        Assert.Equal(control, await ScoreOfWeekThreeAsync(4, 4, 4));
    }

    [Fact]
    public async Task TheAnswerGivenWithCompleting_IsStored()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, null, null);

        await CompleteAsync(database, seed.WeekThreeSession, 4);

        await using var read = database.NewContext();
        Assert.Equal(4, (await read.WorkoutSessions.SingleAsync(session => session.Id == seed.WeekThreeSession)).PerceivedFatigue);
    }

    [Fact]
    public async Task NoAnswer_IsStoredAsNoAnswer()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, null, null);

        await CompleteAsync(database, seed.WeekThreeSession, null);

        await using var read = database.NewContext();
        Assert.Null((await read.WorkoutSessions.SingleAsync(session => session.Id == seed.WeekThreeSession)).PerceivedFatigue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-3)]
    public async Task AnAnswerOutsideOneToFive_IsRefused_AndTheWorkoutIsNotCompleted(int rating)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, null, null);

        var error = await Assert.ThrowsAsync<TrainingLogException>(() => CompleteAsync(database, seed.WeekThreeSession, rating));

        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
        await using var read = database.NewContext();
        var session = await read.WorkoutSessions.SingleAsync(candidate => candidate.Id == seed.WeekThreeSession);
        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Null(session.PerceivedFatigue);
    }

    /// <summary>
    /// Osnova su ranije nedelje istog bloka: odgovori iz tuđeg bloka (ili tuđeg naloga) ne smeju da je pomere.
    /// </summary>
    [Fact]
    public async Task TheBaseline_IsReadFromTheSameBlockOnly()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, 2, 3);

        // Drugi blok istog vežbača sa odgovorom 1 u svakoj nedelji: da je osnova čitana odatle, porast bi bio veći.
        await using (var db = database.NewContext())
        {
            var other = new Mesocycle
            {
                Id = Guid.NewGuid(),
                UserId = database.UserId,
                Name = "Stari blok",
                Goal = Goal.Hypertrophy,
                PeriodizationModel = PeriodizationModel.LinearRising,
                SetAllocation = SetAllocation.FollowTemplate,
                ExperienceLevel = ExperienceLevel.Intermediate,
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            for (var weekNumber = 1; weekNumber <= 2; weekNumber++)
            {
                var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber };
                week.Sessions.Add(new WorkoutSession
                {
                    Id = Guid.NewGuid(),
                    DayLabel = "Day A",
                    Status = SessionStatus.Completed,
                    PerceivedFatigue = 1
                });
                other.Weeks.Add(week);
            }

            db.Mesocycles.Add(other);
            await db.SaveChangesAsync();
        }

        await CompleteAsync(database, seed.WeekThreeSession, 4);

        await using var read = database.NewContext();
        var score = (await read.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekThree)).FatigueScore!.Value;

        Assert.Equal(await ScoreOfWeekThreeAsync(2, 3, 4), score);
    }

    // ---- priprema ------------------------------------------------------------------------

    private static async Task<decimal> ScoreOfWeekThreeAsync(int? weekOne, int? weekTwo, int? weekThree)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, weekOne, weekTwo);

        await CompleteAsync(database, seed.WeekThreeSession, weekThree);

        await using var read = database.NewContext();
        return (await read.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekThree)).FatigueScore!.Value;
    }

    private static async Task CompleteAsync(SqliteDatabase database, Guid sessionId, int? rating)
    {
        await using var context = database.NewContext();
        await SessionCompletionTests.NewService(context).CompleteAsync(database.UserId, sessionId, rating, CancellationToken.None);
    }

    private sealed record Seed(Guid WeekThree, Guid WeekThreeSession);

    /// <summary>
    /// Linearni blok hipertrofije od šest nedelja, jedan trening ("Day A") nedeljno. Prve dve nedelje su odrađene i
    /// ocenjene (sa datim odgovorima), treća je u toku sa tri serije po planu (RIR 1, vrh opsega nije dostignut), a
    /// ostale čekaju. Serije su iste u svim nedeljama, pa jedini signal koji se razlikuje je odgovor.
    /// </summary>
    private static async Task<Seed> SeedAsync(SqliteDatabase database, int? weekOneAnswer, int? weekTwoAnswer)
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

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            PeriodizationModel = PeriodizationModel.LinearRising,
            SetAllocation = SetAllocation.FollowTemplate,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        Guid weekThree = default, sessionThree = default;
        var setLogs = new List<SetLog>();

        for (var weekNumber = 1; weekNumber <= 6; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 6 };
            if (weekNumber <= 2)
            {
                // Već ocenjena nedelja: ocena nije predmet ovog testa, a mora da postoji da je ne bi računao iznova.
                week.FatigueScore = 0m;
                week.VolumeAdaptedAt = DateTime.UtcNow;
            }

            mesocycle.Weeks.Add(week);

            var session = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber <= 2 ? SessionStatus.Completed : weekNumber == 3 ? SessionStatus.InProgress : SessionStatus.Planned,
                PerceivedFatigue = weekNumber == 1 ? weekOneAnswer : weekNumber == 2 ? weekTwoAnswer : null
            };
            var plan = new ExercisePlan
            {
                Id = Guid.NewGuid(),
                ExerciseId = squat,
                Order = 1,
                TargetSets = 3,
                PrescribedSets = 3,
                RepRangeMin = 8,
                RepRangeMax = 12,
                BaseRepRangeMin = 8,
                BaseRepRangeMax = 12,
                TargetRir = 1,
                TargetWeightKg = 100m
            };
            session.ExercisePlans.Add(plan);
            week.Sessions.Add(session);

            if (weekNumber <= 3)
            {
                for (var number = 1; number <= 3; number++)
                {
                    setLogs.Add(new SetLog
                    {
                        Id = Guid.NewGuid(),
                        ExercisePlanId = plan.Id,
                        SetNumber = number,
                        WeightKg = 100m,
                        Reps = 10,
                        Rir = 1,
                        PerformedAt = DateTime.UtcNow
                    });
                }
            }

            if (weekNumber == 3)
            {
                weekThree = week.Id;
                sessionThree = session.Id;
            }
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        // Serije preko skupa, a ne preko kolekcije praćenog plana: nov red sa zadatim ključem bi se tamo ponašao kao postojeći.
        db.SetLogs.AddRange(setLogs);
        await db.SaveChangesAsync();

        return new Seed(weekThree, sessionThree);
    }
}
