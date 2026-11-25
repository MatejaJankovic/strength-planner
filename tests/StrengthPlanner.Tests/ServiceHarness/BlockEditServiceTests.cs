using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Dodavanje i uklanjanje vežbe u bloku koji teče (A3): isti dan, od izabranog treninga do kraja
/// bloka, samo u treninzima koji čekaju. Prošlost i drugi dani ostaju.
/// </summary>
public class BlockEditServiceTests
{
    private const PeriodizationModel Model = PeriodizationModel.LinearRising;
    private const ExperienceLevel Level = ExperienceLevel.Intermediate;
    private const Goal BlockGoal = Goal.Hypertrophy;

    private static readonly Guid Chest = Guid.NewGuid();
    private static readonly Guid Triceps = Guid.NewGuid();
    private static readonly Guid Core = Guid.NewGuid();
    private static readonly Guid Bench = Guid.NewGuid();
    private static readonly Guid Pushdown = Guid.NewGuid();
    private static readonly Guid CableFly = Guid.NewGuid();
    private static readonly Guid Plank = Guid.NewGuid();

    // ---- dodavanje -----------------------------------------------------------------------

    [Fact]
    public async Task AnAddedExercise_ReachesEveryPendingSessionOfTheDay_WithThePrescriptionOfThatWeek()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            var sessionId = await SessionId(context, "Day A", 2);
            var returned = await Service(context).AddExerciseAsync(database.UserId, sessionId, CableFly);
            Assert.Equal(sessionId, returned);
        }

        await using var read = database.NewContext();
        var fly = read.ExercisePlans
            .Where(plan => plan.ExerciseId == CableFly)
            .Select(plan => new
            {
                plan.WorkoutSession.DayLabel,
                plan.WorkoutSession.TrainingWeek.WeekNumber,
                plan.Order,
                plan.TargetSets,
                plan.PrescribedSets,
                plan.RepRangeMin,
                plan.RepRangeMax,
                plan.BaseRepRangeMin,
                plan.BaseRepRangeMax,
                plan.TargetRir,
                plan.TargetWeightKg
            })
            .ToList()
            .OrderBy(plan => plan.WeekNumber)
            .ToList();

        // Šesta nedelja je deload, pa i ona dobija vežbu: od druge do šeste, i ni dana B ni prve nedelje.
        Assert.Equal([2, 3, 4, 5, 6], fly.Select(plan => plan.WeekNumber));
        Assert.All(fly, plan => Assert.Equal("Day A", plan.DayLabel));

        foreach (var plan in fly)
        {
            var expected = ExercisePrescriptions.For(BlockGoal, ExerciseType.Isolation, true, Model, Level, plan.WeekNumber);

            Assert.Equal(3, plan.Order);
            Assert.Equal(expected.Week.Sets, plan.PrescribedSets);
            Assert.Equal(expected.Week.Sets, plan.TargetSets);
            Assert.Equal(expected.Week.RepRangeMin, plan.RepRangeMin);
            Assert.Equal(expected.Week.RepRangeMax, plan.RepRangeMax);
            Assert.Equal(expected.BaseRepRangeMin, plan.BaseRepRangeMin);
            Assert.Equal(expected.BaseRepRangeMax, plan.BaseRepRangeMax);
            Assert.Equal(expected.Week.TargetRir, plan.TargetRir);
            Assert.Null(plan.TargetWeightKg);
        }

        // Propis zavisi od nedelje: da je svuda isti, ovo ne bi ništa dokazivalo.
        Assert.True(fly.Select(plan => (plan.PrescribedSets, plan.RepRangeMax, plan.TargetRir)).Distinct().Count() > 1);
    }

    [Fact]
    public async Task AnAddedExercise_DoesNotTouchAFinishedSessionOrAnotherDay()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 2), CableFly);
        }

        await using var read = database.NewContext();
        Assert.False(read.ExercisePlans.Any(plan =>
            plan.ExerciseId == CableFly && plan.WorkoutSession.TrainingWeek.WeekNumber == 1));
        Assert.False(read.ExercisePlans.Any(plan =>
            plan.ExerciseId == CableFly && plan.WorkoutSession.DayLabel == "Day B"));
    }

    /// <summary>
    /// Dodavanje gleda unapred: trening istog dana u ranijoj nedelji, koji još čeka, ostaje kakav je,
    /// kao i kod zamene vežbe.
    /// </summary>
    [Fact]
    public async Task AnEarlierPendingSession_DoesNotGetTheExercise()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 4), CableFly);
        }

        await using var read = database.NewContext();
        Assert.Equal(
            [4, 5, 6],
            read.ExercisePlans
                .Where(plan => plan.ExerciseId == CableFly)
                .Select(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
                .ToList()
                .Order());
    }

    /// <summary>
    /// Trening kome je upisana serija je počet, ma šta stoji u njegovom statusu (isto pravilo kao kod
    /// balansiranja nedelje): dodata vežba mu ne menja sadržaj dok ga vežbač radi.
    /// </summary>
    [Fact]
    public async Task APlannedSessionThatAlreadyHasALoggedSet_DoesNotGetTheExercise()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            context.SetLogs.Add(new SetLog
            {
                Id = Guid.NewGuid(),
                ExercisePlanId = await PlanId(context, "Day A", 3, Bench),
                SetNumber = 1,
                WeightKg = 80m,
                Reps = 8,
                Rir = 2,
                PerformedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 2), CableFly);
        }

        await using var read = database.NewContext();
        Assert.Equal(
            [2, 4, 5, 6],
            read.ExercisePlans
                .Where(plan => plan.ExerciseId == CableFly)
                .Select(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
                .ToList()
                .Order());
    }

    [Fact]
    public async Task AddingToAWeek_RebalancesIt_WhenTheBlockAimsAtTheVolumeTarget()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.TargetVolume);

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 2), CableFly);
        }

        await using var read = database.NewContext();
        var weekTwo = read.ExercisePlans
            .Where(plan => plan.WorkoutSession.TrainingWeek.WeekNumber == 2 && plan.WorkoutSession.DayLabel == "Day A")
            .ToList();

        // Grudi imaju malu zonu (MAV 6): bench i dodata vežba ne mogu zajedno da nose ono što propis
        // traži od svake ponaosob, pa balansiranje spušta bar jednu ispod propisanog.
        var chest = weekTwo.Where(plan => plan.ExerciseId == Bench || plan.ExerciseId == CableFly).ToList();
        Assert.True(chest.Sum(plan => plan.TargetSets) < chest.Sum(plan => plan.PrescribedSets));
    }

    [Fact]
    public async Task AFollowTemplateBlock_KeepsThePrescription_AfterAnAddition()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 2), CableFly);
        }

        await using var read = database.NewContext();
        Assert.All(
            read.ExercisePlans.Where(plan => plan.WorkoutSession.TrainingWeek.WeekNumber == 2).ToList(),
            plan => Assert.Equal(plan.PrescribedSets, plan.TargetSets));
    }

    [Fact]
    public async Task OnlyLoadableExercisesNotYetInTheSession_AreOffered()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var offered = await Service(context)
            .GetAddableAsync(database.UserId, await SessionId(context, "Day A", 2));

        // Bench i Pushdown su već u danu, a plank nema opterećenje koje bi plan mogao da propiše.
        Assert.Null(offered.Unavailable);
        Assert.Equal(["Cable Fly"], offered.Exercises.Select(exercise => exercise.Name));
        Assert.Equal(["Chest"], offered.Exercises.Single().PrimaryMuscles);
    }

    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Skipped)]
    public async Task ASessionThatIsOver_CannotBeEdited(SessionStatus status)
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            var session = await context.WorkoutSessions.SingleAsync(candidate =>
                candidate.DayLabel == "Day A" && candidate.TrainingWeek.WeekNumber == 2);
            session.Status = status;
            await context.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var sessionId = await SessionId(read, "Day A", 2);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(read).AddExerciseAsync(database.UserId, sessionId, CableFly));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    [Fact]
    public async Task AnExerciseTheSessionAlreadyHas_IsNotAddedTwice()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var sessionId = await SessionId(context, "Day A", 2);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).AddExerciseAsync(database.UserId, sessionId, Bench));

        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
    }

    [Fact]
    public async Task AnExerciseWithoutALoad_IsNotAdded()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var sessionId = await SessionId(context, "Day A", 2);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).AddExerciseAsync(database.UserId, sessionId, Plank));

        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
    }

    [Fact]
    public async Task ADayAtTheLimit_TakesNoMoreExercises()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            // Day C druge nedelje ima najviše vežbi koje dan sme da ima.
            var dayC = await context.WorkoutSessions
                .Include(session => session.ExercisePlans)
                .SingleAsync(session => session.DayLabel == "Day A" && session.TrainingWeek.WeekNumber == 3);

            for (var index = dayC.ExercisePlans.Count; index < TrainingConstants.MaxTemplateExercisesPerDay; index++)
            {
                var filler = Guid.NewGuid();
                context.Exercises.Add(NewExercise(filler, $"Filler {index}", ExerciseType.Isolation, Chest));
                // Preko skupa, a ne preko kolekcije praćenog treninga: nov red sa zadatim ključem bi se
                // tamo ponašao kao postojeći.
                var plan = Plan(filler, index + 1, 3);
                plan.WorkoutSessionId = dayC.Id;
                context.ExercisePlans.Add(plan);
            }

            await context.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var sessionId = await SessionId(read, "Day A", 3);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(read).AddExerciseAsync(database.UserId, sessionId, CableFly));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    /// <summary>
    /// Posle deload-a zbog umora oslobođena planirana nedelja nosi fazu one koja je postala deload,
    /// pa propis po broju nedelje nije onaj koji nedelja nosi. Zato se u takav blok ne dodaje.
    /// </summary>
    [Fact]
    public async Task ABlockThatHadAnAutoDeload_TakesNoNewExercise()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            var week = await context.TrainingWeeks.SingleAsync(candidate => candidate.WeekNumber == 3);
            week.IsDeload = true;
            week.IsAutoDeload = true;
            await context.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var sessionId = await SessionId(read, "Day A", 4);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(read).AddExerciseAsync(database.UserId, sessionId, CableFly));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
        Assert.Equal(0, await read.ExercisePlans.CountAsync(plan => plan.ExerciseId == CableFly));

        // Razlog stiže i kao podatak, jer poruka izuzetka u produkciji ne stiže do ekrana.
        var offered = await Service(read).GetAddableAsync(database.UserId, sessionId);
        Assert.Empty(offered.Exercises);
        Assert.Contains("rasterećenje zbog umora", offered.Unavailable);
    }

    [Fact]
    public async Task AnotherUsersSessionOrExercise_IsOutOfReach()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);
        var foreignFly = Guid.NewGuid();

        await using (var db = database.NewContext())
        {
            var foreign = NewExercise(foreignFly, "Box Fly", ExerciseType.Isolation, Chest);
            foreign.IsCustom = true;
            foreign.CreatedByUserId = Guid.NewGuid();
            db.Exercises.Add(foreign);
            await db.SaveChangesAsync();
        }

        await using var context = database.NewContext();
        var service = Service(context);
        var sessionId = await SessionId(context, "Day A", 2);

        var notMine = await Assert.ThrowsAsync<TrainingLogException>(() =>
            service.AddExerciseAsync(Guid.NewGuid(), sessionId, CableFly));
        Assert.Equal(TrainingLogErrorType.NotFound, notMine.ErrorType);

        Assert.DoesNotContain(
            (await service.GetAddableAsync(database.UserId, sessionId)).Exercises,
            exercise => exercise.ExerciseId == foreignFly);
        var foreignExercise = await Assert.ThrowsAsync<TrainingLogException>(() =>
            service.AddExerciseAsync(database.UserId, sessionId, foreignFly));
        Assert.Equal(TrainingLogErrorType.Validation, foreignExercise.ErrorType);
    }

    // ---- uklanjanje ----------------------------------------------------------------------

    [Fact]
    public async Task ARemovedExercise_LeavesEveryPendingSessionOfTheDay_AndTheOrderClosesUp()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using (var context = database.NewContext())
        {
            var benchWeekTwo = await PlanId(context, "Day A", 2, Bench);
            var returned = await Service(context).RemoveExerciseAsync(database.UserId, benchWeekTwo);
            Assert.Equal(await SessionId(context, "Day A", 2), returned);
        }

        await using var read = database.NewContext();
        var dayA = read.ExercisePlans
            .Where(plan => plan.WorkoutSession.DayLabel == "Day A")
            .Select(plan => new { plan.WorkoutSession.TrainingWeek.WeekNumber, plan.ExerciseId, plan.Order })
            .ToList();

        // Prva nedelja je odrađena i ostaje sa benčem; od druge nadalje bench nema, a Pushdown je prvi.
        Assert.Contains(dayA, plan => plan.WeekNumber == 1 && plan.ExerciseId == Bench && plan.Order == 1);
        Assert.DoesNotContain(dayA, plan => plan.WeekNumber >= 2 && plan.ExerciseId == Bench);
        Assert.All(
            dayA.Where(plan => plan.WeekNumber >= 2),
            plan =>
            {
                Assert.Equal(Pushdown, plan.ExerciseId);
                Assert.Equal(1, plan.Order);
            });

        // Dan B ima isti bench i ne menja se.
        Assert.True(read.ExercisePlans.Any(plan => plan.WorkoutSession.DayLabel == "Day B" && plan.ExerciseId == Bench));
    }

    [Fact]
    public async Task RemovingTheOnlyExerciseOfADay_IsRefused()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var onlyExercise = await PlanId(context, "Day B", 2, Bench);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).RemoveExerciseAsync(database.UserId, onlyExercise));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
        Assert.True(await context.ExercisePlans.AnyAsync(plan => plan.Id == onlyExercise));
    }

    [Fact]
    public async Task AnExerciseWithALoggedSet_IsNotRemoved()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var logged = await PlanId(context, "Day A", 1, Bench);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).RemoveExerciseAsync(database.UserId, logged));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    /// <summary>
    /// Trening u toku sa serijom na drugoj vežbi sme da izgubi vežbu na kojoj još nema serije, a ono što je
    /// odrađeno ostaje.
    /// </summary>
    [Fact]
    public async Task ASessionInProgress_LosesAnUntouchedExercise_AndKeepsWhatWasLogged()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        Guid pushdownPlan;
        await using (var context = database.NewContext())
        {
            var session = await context.WorkoutSessions
                .Include(candidate => candidate.ExercisePlans)
                .SingleAsync(candidate => candidate.DayLabel == "Day A" && candidate.TrainingWeek.WeekNumber == 2);
            session.Status = SessionStatus.InProgress;
            var bench = session.ExercisePlans.Single(plan => plan.ExerciseId == Bench);
            pushdownPlan = session.ExercisePlans.Single(plan => plan.ExerciseId == Pushdown).Id;
            context.SetLogs.Add(new SetLog
            {
                Id = Guid.NewGuid(),
                ExercisePlanId = bench.Id,
                SetNumber = 1,
                WeightKg = 80m,
                Reps = 8,
                Rir = 2,
                PerformedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.NewContext())
        {
            await Service(context).RemoveExerciseAsync(database.UserId, pushdownPlan);
        }

        await using var read = database.NewContext();
        var remaining = read.ExercisePlans
            .Where(plan => plan.WorkoutSession.DayLabel == "Day A" && plan.WorkoutSession.TrainingWeek.WeekNumber == 2)
            .ToList();
        Assert.Equal([Bench], remaining.Select(plan => plan.ExerciseId));
        Assert.Equal(
            1,
            await read.SetLogs.CountAsync(set =>
                set.ExercisePlan.ExerciseId == Bench && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == 2));
    }

    [Fact]
    public async Task AnotherUsersPlan_CannotBeRemoved()
    {
        using var database = new SqliteDatabase();
        await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var plan = await PlanId(context, "Day A", 2, Pushdown);
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).RemoveExerciseAsync(Guid.NewGuid(), plan));

        Assert.Equal(TrainingLogErrorType.NotFound, error.ErrorType);
    }

    // ---- deload posle izmene -------------------------------------------------------------

    /// <summary>
    /// Deload polovi polazni broj serija bloka. Dodata vežba u osnovnoj nedelji nema red, pa se polazni
    /// broj izvodi iz njenog propisa: isti broj kao da je vežba od početka bila u šablonu.
    /// </summary>
    [Fact]
    public async Task AnAutoDeload_HalvesTheBaseOfAnAddedExercise()
    {
        using var database = new SqliteDatabase();
        var mesocycleId = await SeedStrengthBlockAsync(database);

        await using (var context = database.NewContext())
        {
            await Service(context).AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 4), CableFly);
        }

        await CompleteHeavyWeekFourAsync(database);
        await EvaluateAsync(database, mesocycleId);

        await using var read = database.NewContext();
        var deloaded = read.ExercisePlans.Single(plan =>
            plan.ExerciseId == CableFly && plan.WorkoutSession.TrainingWeek.WeekNumber == 5);

        Assert.True(read.TrainingWeeks.Single(week => week.WeekNumber == 5).IsDeload);
        Assert.Equal(Periodization.DeloadSets(ExperienceProgramming.StartingSetsPerExercise(Level)), deloaded.TargetSets);
    }

    /// <summary>
    /// Uklonjena vežba ispred je zbila redosled, pa dodata vežba nosi redni broj koji je u osnovnoj nedelji
    /// imala druga vežba - koja je i dalje tu. Njen polazni broj serija nije polazni broj dodate vežbe.
    /// </summary>
    [Fact]
    public async Task AnAddedExercise_DoesNotInheritTheBaseOfAnExerciseThatStillStandsInItsPlace()
    {
        using var database = new SqliteDatabase();
        var mesocycleId = await SeedStrengthBlockAsync(database, curlBaseSets: 6);

        await using (var context = database.NewContext())
        {
            var service = Service(context);
            // Dan A: Bench(1), Pushdown(2), Curl(3). Pushdown ide, Curl se zbija na 2, a dodata vežba dobija 3.
            await service.RemoveExerciseAsync(database.UserId, await PlanId(context, "Day A", 4, Pushdown));
            await service.AddExerciseAsync(database.UserId, await SessionId(context, "Day A", 4), CableFly);
        }

        await CompleteHeavyWeekFourAsync(database);
        await EvaluateAsync(database, mesocycleId);

        await using var read = database.NewContext();
        var deloaded = read.ExercisePlans.Single(plan =>
            plan.ExerciseId == CableFly && plan.WorkoutSession.TrainingWeek.WeekNumber == 5);

        Assert.Equal(3, deloaded.Order);
        Assert.Equal(Periodization.DeloadSets(ExperienceProgramming.StartingSetsPerExercise(Level)), deloaded.TargetSets);
        Assert.NotEqual(Periodization.DeloadSets(6), deloaded.TargetSets);
    }

    // ---- priprema ------------------------------------------------------------------------

    private static BlockEditService Service(AppDbContext context)
    {
        var landmarks = new VolumeLandmarkService(context);
        return new BlockEditService(
            context,
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)));
    }

    private static async Task<Guid> SessionId(AppDbContext context, string day, int week)
    {
        return await context.WorkoutSessions
            .Where(session => session.DayLabel == day && session.TrainingWeek.WeekNumber == week)
            .Select(session => session.Id)
            .SingleAsync();
    }

    private static async Task<Guid> PlanId(AppDbContext context, string day, int week, Guid exerciseId)
    {
        return await context.ExercisePlans
            .Where(plan => plan.WorkoutSession.DayLabel == day
                           && plan.WorkoutSession.TrainingWeek.WeekNumber == week
                           && plan.ExerciseId == exerciseId)
            .Select(plan => plan.Id)
            .SingleAsync();
    }

    /// <summary>
    /// Blok od šest nedelja (poslednja je deload). Day A: Bench pa Pushdown; Day B: samo Bench. Prva nedelja je
    /// odrađena (bench ima seriju), ostale čekaju. Planovi nose propis kakav bi ih generisao blok.
    /// </summary>
    private static async Task SeedAsync(SqliteDatabase database, SetAllocation allocation)
    {
        await using var db = database.NewContext();
        SeedCatalog(db);

        var mesocycle = NewMesocycle(database.UserId, allocation, BlockGoal);

        for (var weekNumber = 1; weekNumber <= 6; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 6 };
            mesocycle.Weeks.Add(week);

            var dayA = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber == 1 ? SessionStatus.Completed : SessionStatus.Planned
            };
            var bench = GeneratedPlan(Bench, 1, ExerciseType.Compound, weekNumber);
            dayA.ExercisePlans.Add(bench);
            dayA.ExercisePlans.Add(GeneratedPlan(Pushdown, 2, ExerciseType.Isolation, weekNumber));
            week.Sessions.Add(dayA);

            if (weekNumber == 1)
            {
                bench.SetLogs.Add(new SetLog
                {
                    Id = Guid.NewGuid(),
                    SetNumber = 1,
                    WeightKg = 80m,
                    Reps = 8,
                    Rir = 2,
                    PerformedAt = DateTime.UtcNow
                });
            }

            var dayB = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = "Day B", Status = SessionStatus.Planned };
            dayB.ExercisePlans.Add(GeneratedPlan(Bench, 1, ExerciseType.Compound, weekNumber));
            week.Sessions.Add(dayB);
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Blok snage od šest nedelja za deload: tri i četiri čekaju, osnovna nedelja linearnog modela je treća.
    /// Day A: Bench, Pushdown, Curl; osnovna nedelja nosi Curl sa <paramref name="curlBaseSets"/> serija.
    /// </summary>
    private static async Task<Guid> SeedStrengthBlockAsync(SqliteDatabase database, int curlBaseSets = 3)
    {
        await using var db = database.NewContext();
        SeedCatalog(db);
        var curl = Guid.NewGuid();
        db.Exercises.Add(NewExercise(curl, "Curl", ExerciseType.Isolation, Triceps));

        var mesocycle = NewMesocycle(database.UserId, SetAllocation.FollowTemplate, Goal.Strength);

        for (var weekNumber = 1; weekNumber <= 6; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 6 };
            mesocycle.Weeks.Add(week);

            if (weekNumber < 3)
            {
                continue;
            }

            var session = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber == 3 ? SessionStatus.Completed : SessionStatus.Planned
            };

            // Teška nedelja je četvrta: iste vrednosti kao u SwappedExerciseDeloadTests (opseg 3-6, RIR 2).
            var bench = StrengthPlan(Bench, 1, 4);
            session.ExercisePlans.Add(bench);
            session.ExercisePlans.Add(StrengthPlan(Pushdown, 2, 3));
            session.ExercisePlans.Add(StrengthPlan(curl, 3, weekNumber == 3 ? curlBaseSets : 3));
            week.Sessions.Add(session);
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return mesocycle.Id;
    }

    private static async Task CompleteHeavyWeekFourAsync(SqliteDatabase database)
    {
        await using var db = database.NewContext();
        var session = await db.WorkoutSessions
            .Include(candidate => candidate.ExercisePlans)
            .SingleAsync(candidate => candidate.DayLabel == "Day A" && candidate.TrainingWeek.WeekNumber == 4);
        session.Status = SessionStatus.Completed;

        // Dve serije uz RIR 0 naspram cilja 2 i dva otkaza: 0.35 + 0.25 = 0.60, prag srednjeg nivoa.
        var bench = session.ExercisePlans.Single(plan => plan.ExerciseId == Bench);
        db.SetLogs.AddRange(
            HeavySet(bench.Id, 1, 5, false),
            HeavySet(bench.Id, 2, 5, false),
            HeavySet(bench.Id, 3, 3, true),
            HeavySet(bench.Id, 4, 3, true));
        await db.SaveChangesAsync();
    }

    private static async Task EvaluateAsync(SqliteDatabase database, Guid mesocycleId)
    {
        await using var context = database.NewContext();
        var outcome = await new DeloadService(context, new VolumeLandmarkService(context))
            .EvaluatePendingWeeksAsync(database.UserId, mesocycleId, CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.NotNull(outcome);
    }

    private static void SeedCatalog(AppDbContext db)
    {
        db.MuscleGroups.AddRange(
            new MuscleGroup { Id = Chest, Name = "Chest" },
            new MuscleGroup { Id = Triceps, Name = "Triceps" },
            new MuscleGroup { Id = Core, Name = "Core" });
        db.VolumeLandmarks.AddRange(
            new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = Chest, Mev = 4, Mav = 6, Mrv = 8 },
            new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = Triceps, Mev = 4, Mav = 8, Mrv = 12 },
            new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = Core, Mev = 4, Mav = 8, Mrv = 12 });

        var plank = NewExercise(Plank, "Plank", ExerciseType.Isolation, Core);
        plank.Equipment = "Bodyweight";
        plank.BodyweightShare = 0m;

        db.Exercises.AddRange(
            NewExercise(Bench, "Bench Press", ExerciseType.Compound, Chest),
            NewExercise(Pushdown, "Pushdown", ExerciseType.Isolation, Triceps),
            NewExercise(CableFly, "Cable Fly", ExerciseType.Isolation, Chest),
            plank);
    }

    private static Mesocycle NewMesocycle(Guid userId, SetAllocation allocation, Goal goal)
    {
        return new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Blok",
            Goal = goal,
            ExperienceLevel = Level,
            PeriodizationModel = Model,
            SetAllocation = allocation,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };
    }

    private static Exercise NewExercise(Guid id, string name, ExerciseType type, Guid primary)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            Type = type,
            Equipment = "Cable",
            WeightStepKg = 2.5m,
            SuitsLowReps = type == ExerciseType.Compound,
            Muscles = [new ExerciseMuscle { MuscleGroupId = primary, Contribution = 1.0m }]
        };
    }

    /// <summary>Plan kakav bi ga generisao blok: propis te nedelje, bez opterećenja.</summary>
    private static ExercisePlan GeneratedPlan(Guid exerciseId, int order, ExerciseType type, int weekNumber)
    {
        var prescribed = ExercisePrescriptions.For(BlockGoal, type, type == ExerciseType.Compound, Model, Level, weekNumber);

        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = order,
            TargetSets = prescribed.Week.Sets,
            PrescribedSets = prescribed.Week.Sets,
            RepRangeMin = prescribed.Week.RepRangeMin,
            RepRangeMax = prescribed.Week.RepRangeMax,
            BaseRepRangeMin = prescribed.BaseRepRangeMin,
            BaseRepRangeMax = prescribed.BaseRepRangeMax,
            TargetRir = prescribed.Week.TargetRir
        };
    }

    private static ExercisePlan Plan(Guid exerciseId, int order, int sets)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = order,
            TargetSets = sets,
            PrescribedSets = sets,
            RepRangeMin = 8,
            RepRangeMax = 12,
            BaseRepRangeMin = 8,
            BaseRepRangeMax = 12,
            TargetRir = 2
        };
    }

    private static ExercisePlan StrengthPlan(Guid exerciseId, int order, int sets)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = order,
            TargetSets = sets,
            PrescribedSets = sets,
            RepRangeMin = 3,
            RepRangeMax = 6,
            BaseRepRangeMin = 3,
            BaseRepRangeMax = 6,
            TargetRir = 2,
            TargetWeightKg = 100m
        };
    }

    private static SetLog HeavySet(Guid planId, int number, int reps, bool isFailure)
    {
        return new SetLog
        {
            Id = Guid.NewGuid(),
            ExercisePlanId = planId,
            SetNumber = number,
            WeightKg = 100m,
            Reps = reps,
            Rir = 0,
            IsFailure = isFailure,
            PerformedAt = DateTime.UtcNow
        };
    }
}
