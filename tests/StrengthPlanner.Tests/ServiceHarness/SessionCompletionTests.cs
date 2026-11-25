using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Završetak treninga je jedna transakcija sa više koraka - procena maksimuma i progresija,
/// zatvaranje nedelje (granice volumena i ocena umora), balansiranje i prelazak na sledeći
/// blok - i do sada nijedan test nije pozvao <c>SessionService.CompleteAsync</c>. Ovi testovi
/// beleže šta ta metoda danas radi, da bi se delila na korake (#117) bez promene ponašanja i
/// da bi isti koraci mogli da posluže ispravci završenog treninga.
/// </summary>
public class SessionCompletionTests
{
    /// <summary>
    /// Tri serije 100 kg x 12 uz RIR 1 u opsegu 8-12: vrh opsega, pa težina ide gore. Druga
    /// vežba nema nijednu seriju, pa u narednu nedelju prenosi planiranih 80 kg.
    /// </summary>
    [Fact]
    public async Task Completing_WritesTheEstimate_AndTheNextWeeksTarget()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);

        var result = await CompleteAsync(database, seed.WeekOneSession);

        Assert.Equal(SessionStatus.Completed.ToString(), result.Status);

        var squat = result.Exercises.Single(summary => summary.ExerciseId == seed.Squat);
        Assert.Equal(100m, squat.UsedWeightKg);
        Assert.NotNull(squat.E1Rm);
        Assert.True(squat.WeightIncreased);
        Assert.Equal(102.5m, squat.NextWeightKg);
        Assert.Equal(2.5m, squat.WeightChangeKg);

        var untouched = result.Exercises.Single(summary => summary.ExerciseId == seed.Hack);
        Assert.Null(untouched.E1Rm);
        Assert.Equal(80m, untouched.UsedWeightKg);
        Assert.Equal(80m, untouched.NextWeightKg);

        await using var read = database.NewContext();
        var nextWeek = await read.ExercisePlans
            .Where(plan => plan.WorkoutSessionId == seed.WeekTwoSession)
            .ToDictionaryAsync(plan => plan.ExerciseId, plan => plan.TargetWeightKg);
        Assert.Equal(102.5m, nextWeek[seed.Squat]);
        Assert.Equal(80m, nextWeek[seed.Hack]);

        // Procena se upisuje kao zasebna, računata vrednost - uz ručni unos od 130 kg.
        var estimates = await read.OneRepMaxRecords
            .Where(record => record.ExerciseId == seed.Squat && record.Source == OneRepMaxSource.Estimated)
            .ToListAsync();
        Assert.Single(estimates);
        Assert.Equal(squat.E1Rm, estimates[0].ValueKg);
    }

    /// <summary>
    /// Nedelja je zatvorena kad je poslednji trening nedelje završen: tada dobija ocenu umora i
    /// obračun granica volumena, oba tačno jednom.
    /// </summary>
    [Fact]
    public async Task Completing_TheLastSessionOfAWeek_ClosesTheWeek()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);

        await CompleteAsync(database, seed.WeekOneSession);

        await using var read = database.NewContext();
        var week = await read.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekOne);
        Assert.NotNull(week.FatigueScore);
        Assert.NotNull(week.VolumeAdaptedAt);

        var untouchedWeek = await read.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekTwo);
        Assert.Null(untouchedWeek.FatigueScore);
        Assert.Null(untouchedWeek.VolumeAdaptedAt);
    }

    /// <summary>
    /// Završen trening se ne završava drugi put: obračun (procena, progresija, granice) bi se
    /// ponovio, a zapisi udvostručili.
    /// </summary>
    [Fact]
    public async Task Completing_ACompletedSession_Conflicts_AndWritesNothing()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);
        await CompleteAsync(database, seed.WeekOneSession);

        var error = await Assert.ThrowsAsync<StrengthPlanner.Application.Exceptions.TrainingLogException>(
            () => CompleteAsync(database, seed.WeekOneSession));

        Assert.Equal(StrengthPlanner.Application.Exceptions.TrainingLogErrorType.Conflict, error.ErrorType);
        await using var read = database.NewContext();
        Assert.Equal(
            1,
            await read.OneRepMaxRecords.CountAsync(record => record.Source == OneRepMaxSource.Estimated));
    }

    /// <summary>
    /// Trening iz drugog naloga se ne vidi: obračun je ograničen po korisniku.
    /// </summary>
    [Fact]
    public async Task Completing_AnotherUsersSession_IsNotFound()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var service = NewService(context);

        var error = await Assert.ThrowsAsync<StrengthPlanner.Application.Exceptions.TrainingLogException>(
            () => service.CompleteAsync(Guid.NewGuid(), seed.WeekOneSession, CancellationToken.None));

        Assert.Equal(StrengthPlanner.Application.Exceptions.TrainingLogErrorType.NotFound, error.ErrorType);
    }

    /// <summary>
    /// Sa balansiranjem ka nedeljnom cilju trening koji se završava prolazi i kroz
    /// <c>RebalanceAsync</c>: preostali dan iste nedelje propisuje deset serija za mišić koji je
    /// već na tri, pa mu se serije skidaju i rezime to prijavljuje.
    /// </summary>
    [Fact]
    public async Task Completing_WithTargetVolume_CutsTheRestOfTheWeekToTheTarget()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.TargetVolume, secondDayInWeekOne: true);

        var result = await CompleteAsync(database, seed.WeekOneSession);

        var change = Assert.Single(result.VolumeAdjustments);
        Assert.Equal("Day B", change.DayLabel);
        Assert.Equal(10, change.FromSets);
        Assert.True(change.ToSets < change.FromSets);

        await using var read = database.NewContext();
        var dayB = await read.ExercisePlans.SingleAsync(plan => plan.WorkoutSession.DayLabel == "Day B");
        Assert.Equal(change.ToSets, dayB.TargetSets);
        Assert.Equal(10, dayB.PrescribedSets);
    }

    /// <summary>
    /// Blok koji prati šablon doslovno se ne balansira ni pri završetku treninga: izbor važi i
    /// posle prvog treninga, ne samo do njega.
    /// </summary>
    [Fact]
    public async Task Completing_WhileFollowingTheTemplate_LeavesTheRestOfTheWeekAlone()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate, secondDayInWeekOne: true);

        var result = await CompleteAsync(database, seed.WeekOneSession);

        Assert.Empty(result.VolumeAdjustments);
        await using var read = database.NewContext();
        var dayB = await read.ExercisePlans.SingleAsync(plan => plan.WorkoutSession.DayLabel == "Day B");
        Assert.Equal(10, dayB.TargetSets);
    }

    /// <summary>
    /// Izdvojeni korak sam: naredni planovi su istog dana u kasnijim nedeljama, a već završen
    /// trening se ne dira (završetak van redosleda ne sme da prepiše odrađene ciljeve).
    /// </summary>
    [Fact]
    public async Task TheNextPlans_AreTheSameDayLaterWeeks_AndNeverACompletedSession()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var db = database.NewContext())
        {
            var weekTwoSession = await db.WorkoutSessions.SingleAsync(session => session.Id == seed.WeekTwoSession);
            weekTwoSession.Status = SessionStatus.Completed;
            await db.SaveChangesAsync();
        }

        await using var context = database.NewContext();
        var session = await context.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
            .Include(workoutSession => workoutSession.ExercisePlans)
            .SingleAsync(workoutSession => workoutSession.Id == seed.WeekOneSession);

        var nextPlans = await new SessionOutcomes(context).LoadNextPlansAsync(session, CancellationToken.None);

        Assert.Empty(nextPlans);
    }

    /// <summary>
    /// Izdvojeni korak sam: upisuje cilj naredne nedelje i procenu, a rezime tek posle
    /// <c>Finalize</c> čita ono što je zaista upisano - ne ono što je progresija predložila.
    /// </summary>
    [Fact]
    public async Task TheSummary_ReadsTheStoredTarget_OnlyAfterFinalize()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var session = await context.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.Exercise)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.SetLogs)
            .SingleAsync(workoutSession => workoutSession.Id == seed.WeekOneSession);
        var outcomes = new SessionOutcomes(context);
        var nextPlans = await outcomes.LoadNextPlansAsync(session, CancellationToken.None);

        var outcome = await outcomes.ApplyAsync(
            database.UserId,
            session,
            nextPlans,
            DateTime.UtcNow,
            CancellationToken.None);

        var squat = outcome.Summaries.Single(summary => summary.ExerciseId == seed.Squat);
        Assert.Null(squat.NextWeightKg);

        // Deload ili balansiranje bi sada mogli da prepišu cilj na istom entitetu.
        nextPlans.Single(plan => plan.ExerciseId == seed.Squat).TargetWeightKg = 90m;
        outcome.Finalize();

        Assert.Equal(90m, squat.NextWeightKg);
        Assert.Equal(-10m, squat.WeightChangeKg);
        Assert.False(squat.WeightIncreased);
    }

    private static async Task<CompleteSessionResultDto> CompleteAsync(SqliteDatabase database, Guid sessionId)
    {
        await using var context = database.NewContext();
        return await NewService(context).CompleteAsync(database.UserId, sessionId, CancellationToken.None);
    }

    internal static SessionService NewService(AppDbContext context)
    {
        var landmarks = new VolumeLandmarkService(context);
        return new SessionService(
            context,
            landmarks,
            new DeloadService(context, landmarks),
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)),
            // Mezociklus ovde nije u bloku, pa pomoćni servis plana nikad ne dolazi do generatora.
            new MacrocycleService(context, null!, null!));
    }

    private sealed record Seed(
        Guid Squat,
        Guid Hack,
        Guid WeekOne,
        Guid WeekTwo,
        Guid WeekOneSession,
        Guid WeekTwoSession);

    /// <summary>
    /// Blok hipertrofije od četiri nedelje, jedan trening ("Day A") nedeljno. U prvoj nedelji
    /// trening je u toku, u drugoj planiran. Čučanj ima tri odrađene serije, druga vežba nijednu.
    /// </summary>
    private static async Task<Seed> SeedAsync(
        SqliteDatabase database,
        SetAllocation allocation,
        bool secondDayInWeekOne = false)
    {
        var quads = Guid.NewGuid();
        var squat = Guid.NewGuid();
        var hack = Guid.NewGuid();

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
        db.Exercises.AddRange(
            new Exercise
            {
                Id = squat,
                Name = "Back Squat",
                Type = ExerciseType.Compound,
                Equipment = "Barbell",
                WeightStepKg = 2.5m,
                Muscles = [new ExerciseMuscle { MuscleGroupId = quads, Contribution = 1.0m }]
            },
            new Exercise
            {
                Id = hack,
                Name = "Hack Squat",
                Type = ExerciseType.Compound,
                Equipment = "Machine",
                WeightStepKg = 5m,
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
            SetAllocation = allocation,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        Guid weekOne = default, weekTwo = default, sessionOne = default, sessionTwo = default;
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
            var squatPlan = Plan(squat, order: 1, weightKg: 100m);
            var hackPlan = Plan(hack, order: 2, weightKg: 80m);
            if (weekNumber == 1)
            {
                for (var number = 1; number <= 3; number++)
                {
                    squatPlan.SetLogs.Add(new SetLog
                    {
                        Id = Guid.NewGuid(),
                        SetNumber = number,
                        WeightKg = 100m,
                        Reps = 12,
                        Rir = 1,
                        PerformedAt = DateTime.UtcNow
                    });
                }
            }

            session.ExercisePlans.Add(squatPlan);
            session.ExercisePlans.Add(hackPlan);
            week.Sessions.Add(session);

            if (weekNumber == 1 && secondDayInWeekOne)
            {
                // Drugi dan iste nedelje propisuje deset serija za isti mišić: sa tri odrađene
                // to je trinaest, iznad cilja od 12 (MAV), pa balansiranje ima šta da skine.
                var dayB = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = "Day B", Status = SessionStatus.Planned };
                var heavyPlan = Plan(squat, order: 1, weightKg: 100m);
                heavyPlan.TargetSets = 10;
                heavyPlan.PrescribedSets = 10;
                dayB.ExercisePlans.Add(heavyPlan);
                week.Sessions.Add(dayB);
            }

            if (weekNumber == 1)
            {
                weekOne = week.Id;
                sessionOne = session.Id;
            }
            else
            {
                weekTwo = week.Id;
                sessionTwo = session.Id;
            }
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new Seed(squat, hack, weekOne, weekTwo, sessionOne, sessionTwo);
    }

    private static ExercisePlan Plan(Guid exerciseId, int order, decimal weightKg)
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
            TargetRir = 1,
            TargetWeightKg = weightKg
        };
    }
}
