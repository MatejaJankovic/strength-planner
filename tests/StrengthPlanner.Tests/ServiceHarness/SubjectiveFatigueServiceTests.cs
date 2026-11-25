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
        var seed = await SeedAsync(database, [null, null]);

        await CompleteAsync(database, seed.Session, 4);

        await using var read = database.NewContext();
        Assert.Equal(4, (await read.WorkoutSessions.SingleAsync(session => session.Id == seed.Session)).PerceivedFatigue);
    }

    [Fact]
    public async Task NoAnswer_IsStoredAsNoAnswer()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, [null, null]);

        await CompleteAsync(database, seed.Session, null);

        await using var read = database.NewContext();
        Assert.Null((await read.WorkoutSessions.SingleAsync(session => session.Id == seed.Session)).PerceivedFatigue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-3)]
    public async Task AnAnswerOutsideOneToFive_IsRefused_AndTheWorkoutIsNotCompleted(int rating)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, [null, null]);

        var error = await Assert.ThrowsAsync<TrainingLogException>(() => CompleteAsync(database, seed.Session, rating));

        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
        await using var read = database.NewContext();
        var session = await read.WorkoutSessions.SingleAsync(candidate => candidate.Id == seed.Session);
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
        var seed = await SeedAsync(database, [2, 3]);

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

        await CompleteAsync(database, seed.Session, 4);

        await using var read = database.NewContext();
        var score = (await read.TrainingWeeks.SingleAsync(week => week.Id == seed.Week)).FatigueScore!.Value;

        Assert.Equal(await ScoreOfWeekThreeAsync(2, 3, 4), score);
    }

    /// <summary>
    /// Nedelja rasterećenja usred bloka se ne broji u osnovu: vežbač se u njoj oseća bolje, pa bi spustila
    /// osnovu i naduvala porast sledeće nedelje. Odgovori 2, 3, (rasterećenje) 1, pa 3: bez rasterećenja je
    /// osnova 2.5 i porast 0.5 (signal na pola težine, 0.10); sa njim bi osnova bila 2 i porast 1.0 (0.20).
    /// </summary>
    [Fact]
    public async Task ADeloadWeekInTheMiddle_DoesNotLowerTheBaseline()
    {
        var control = await ScoreOfWeekAsync([null, null, null], null, deloadWeek: 3);
        var score = await ScoreOfWeekAsync([2, 3, 1], 3, deloadWeek: 3);

        Assert.Equal(control + 0.10m, score);
    }

    // ---- priprema ------------------------------------------------------------------------

    private static Task<decimal> ScoreOfWeekThreeAsync(int? weekOne, int? weekTwo, int? weekThree)
    {
        return ScoreOfWeekAsync([weekOne, weekTwo], weekThree);
    }

    /// <summary>
    /// Ocena nedelje koja se završava (posle nedelja sa datim odgovorima). <paramref name="deloadWeek"/> je
    /// nedelja rasterećenja usred bloka; njen odgovor je u spisku kao i svaki drugi.
    /// </summary>
    private static async Task<decimal> ScoreOfWeekAsync(IReadOnlyList<int?> earlierAnswers, int? answer, int? deloadWeek = null)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, earlierAnswers, deloadWeek);

        await CompleteAsync(database, seed.Session, answer);

        await using var read = database.NewContext();
        return (await read.TrainingWeeks.SingleAsync(week => week.Id == seed.Week)).FatigueScore!.Value;
    }

    private static async Task CompleteAsync(SqliteDatabase database, Guid sessionId, int? rating)
    {
        await using var context = database.NewContext();
        await SessionCompletionTests.NewService(context).CompleteAsync(database.UserId, sessionId, rating, CancellationToken.None);
    }

    /// <summary>Nedelja koja se završava i njen trening.</summary>
    private sealed record Seed(Guid Week, Guid Session);

    /// <summary>
    /// Linearni blok hipertrofije od šest nedelja, jedan trening ("Day A") nedeljno. Nedelje sa datim odgovorima su
    /// odrađene i ocenjene, sledeća je u toku sa tri serije po planu (RIR 1, vrh opsega nije dostignut), a ostale
    /// čekaju. Serije su iste u svim nedeljama, pa jedini signal koji se razlikuje je odgovor. Poslednja nedelja
    /// je rasterećenje, osim ako je <paramref name="deloadWeek"/> druga.
    /// </summary>
    private static async Task<Seed> SeedAsync(SqliteDatabase database, IReadOnlyList<int?> answers, int? deloadWeek = null)
    {
        var current = answers.Count + 1;
        var deload = deloadWeek ?? 6;

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

        Guid currentWeek = default, currentSession = default;
        var setLogs = new List<SetLog>();

        for (var weekNumber = 1; weekNumber <= 6; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == deload };
            if (weekNumber < current)
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
                Status = weekNumber < current ? SessionStatus.Completed : weekNumber == current ? SessionStatus.InProgress : SessionStatus.Planned,
                PerceivedFatigue = weekNumber < current ? answers[weekNumber - 1] : null
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

            if (weekNumber <= current)
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

            if (weekNumber == current)
            {
                currentWeek = week.Id;
                currentSession = session.Id;
            }
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        // Serije preko skupa, a ne preko kolekcije praćenog plana: nov red sa zadatim ključem bi se tamo ponašao kao postojeći.
        db.SetLogs.AddRange(setLogs);
        await db.SaveChangesAsync();

        return new Seed(currentWeek, currentSession);
    }
}
