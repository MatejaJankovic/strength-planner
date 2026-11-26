using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.Templates;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Blok održavanja kroz prave servise (R20.4). Domen je testiran u <c>MaintenanceGoalTests</c>, ali tri pravila žive u
/// pozivima servisa i bez ovoga bi se mogla vratiti uz ceo paket zelen, isto kao kod nivoa iskustva (#74) i izolacije
/// (runda 10): zastavica koja skida korak sa vrha opsega, ćutanje učenja granica i model koji se bloku održavanja
/// nameće pri pravljenju. Svaki test ima blizanca u rastućem bloku, da proveri da se razlika vidi baš zbog cilja.
/// </summary>
public class MaintenanceGoalServiceTests
{
    // ---- težina na vrhu opsega ----------------------------------------------------------------------------------

    /// <summary>
    /// Tri serije na vrhu opsega uz rezervu koju blok traži. U hipertrofiji težina ide gore (102.5), u održavanju stoji
    /// (100): isti trening, ista težina, isti korak, različit cilj bloka.
    /// </summary>
    [Theory]
    [InlineData(Goal.Hypertrophy, 8, 12, 1, 12, 102.5, true)]
    [InlineData(Goal.Maintenance, 6, 10, 3, 10, 100.0, false)]
    public async Task Completing_AtTheTopOfTheRange_RaisesTheLoadOnlyInABlockThatBuilds(
        Goal goal,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        double expectedNextKg,
        bool expectedIncrease)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, goal, repRangeMin, repRangeMax, targetRir, sets: 3, reps, rir: targetRir);

        var result = await CompleteAsync(database, seed.SessionOne);

        var squat = result.Exercises.Single(summary => summary.ExerciseId == seed.Squat);
        Assert.Equal(100m, squat.UsedWeightKg);
        Assert.Equal((decimal)expectedNextKg, squat.NextWeightKg);
        Assert.Equal(expectedIncrease, squat.WeightIncreased);

        await using var read = database.NewContext();
        var nextWeek = await read.ExercisePlans.SingleAsync(plan => plan.WorkoutSessionId == seed.SessionTwo);
        Assert.Equal((decimal)expectedNextKg, nextWeek.TargetWeightKg);
    }

    /// <summary>
    /// Posle rasterećenja (ovde auto-deload u drugoj nedelji) napredovanje se nastavlja iz poslednje trenažne nedelje
    /// istog dana, a ne iz deload serija. To je drugo mesto na kome se računa korak na vrhu opsega, i bez ovog testa
    /// se zastavica na njemu mogla vratiti uz ceo paket zelen (izmereno: 0 od 1814). Treća nedelja: hipertrofija
    /// nastavlja sa 102.5, održavanje sa 100.
    /// </summary>
    [Theory]
    [InlineData(Goal.Hypertrophy, 8, 12, 1, 12, 102.5)]
    [InlineData(Goal.Maintenance, 6, 10, 3, 10, 100.0)]
    public async Task ResumingAfterADeload_RaisesTheLoadOnlyInABlockThatBuilds(
        Goal goal,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        double expectedNextKg)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(
            database,
            goal,
            repRangeMin,
            repRangeMax,
            targetRir,
            sets: 3,
            reps,
            rir: targetRir,
            afterDeload: true);

        await CompleteAsync(database, seed.SessionOne);

        await using var read = database.NewContext();
        var resumed = await read.ExercisePlans.SingleAsync(plan => plan.WorkoutSessionId == seed.SessionTwo);
        Assert.Equal((decimal)expectedNextKg, resumed.TargetWeightKg);
    }

    /// <summary>
    /// Ocena umora radi u bloku održavanja kao i svuda: nedelja sa dve serije do otkaza i dve teške (0.35 + 0.25 = prag
    /// srednjeg nivoa) povlači deload na narednu nedelju. Rasterećenje čita propis iz cilja bloka, što je pre cilja
    /// održavanja bacalo izuzetak: opseg ostaje 6-10, rezerva je cilj plus dva, ali ograničena na najviše četiri
    /// (<c>Periodization.MaxRir</c>), pa je kod rezerve 3 rasterećenje za jedan, a ne dva, i teret je 90% podignutog.
    /// </summary>
    [Fact]
    public async Task ATiredWeek_StillPullsADeload_InAMaintenanceBlock()
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(
            database,
            Goal.Maintenance,
            repRangeMin: 6,
            repRangeMax: 10,
            targetRir: 3,
            sets: 4,
            reps: 8,
            rir: 0,
            heavyWeek: true);

        await CompleteAsync(database, seed.SessionOne);

        await using var read = database.NewContext();
        var weekOne = await read.TrainingWeeks.SingleAsync(week => week.Id == seed.WeekOne);
        Assert.True(weekOne.FatigueScore >= 0.60m, $"score {weekOne.FatigueScore}");

        Assert.True((await read.TrainingWeeks.SingleAsync(week => week.WeekNumber == 2)).IsDeload);

        var deload = await read.ExercisePlans.SingleAsync(plan => plan.WorkoutSessionId == seed.SessionTwo);
        Assert.Equal((6, 10), (deload.RepRangeMin, deload.RepRangeMax));
        Assert.Equal(Periodization.DeloadRir(3), deload.TargetRir);
        Assert.Equal(4, deload.TargetRir);
        Assert.Equal(90m, deload.TargetWeightKg);
    }

    // ---- granice se ne uče --------------------------------------------------------------------------------------

    /// <summary>
    /// Nedelja od deset teških serija (do otkaza) na mišiću čiji je MEV osam. U hipertrofiji to pomera MRV (20 na 19:
    /// nedelja je bar na minimumu i pokazala je umor); u održavanju ne pomera ništa i ne ostavlja ni red pomaka, a
    /// nedelja je ipak obračunata.
    /// </summary>
    [Theory]
    [InlineData(Goal.Hypertrophy, 8, 12, 1, 19)]
    [InlineData(Goal.Maintenance, 6, 10, 3, null)]
    public async Task CompletingAWeek_MovesTheLimits_OnlyInABlockThatBuilds(
        Goal goal,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int? expectedMrv)
    {
        using var database = new SqliteDatabase();
        var seed = await SeedAsync(database, goal, repRangeMin, repRangeMax, targetRir, sets: 10, reps: 8, rir: 0);

        await CompleteAsync(database, seed.SessionOne);

        await using var read = database.NewContext();
        var learned = await read.UserVolumeLandmarks
            .Where(landmark => landmark.UserId == database.UserId)
            .ToListAsync();

        if (expectedMrv is null)
        {
            Assert.Empty(learned);
            Assert.Empty(await read.VolumeLandmarkChanges.ToListAsync());
        }
        else
        {
            Assert.Equal(expectedMrv.Value, Assert.Single(learned).Mrv);
            Assert.Single(await read.VolumeLandmarkChanges.ToListAsync());
        }

        // Obračunata je i tiha nedelja: inače bi se vraćala među neobračunate pri svakom završetku treninga.
        var week = await read.TrainingWeeks.SingleAsync(candidate => candidate.Id == seed.WeekOne);
        Assert.NotNull(week.VolumeAdaptedAt);
    }

    // ---- pravljenje bloka ---------------------------------------------------------------------------------------

    /// <summary>
    /// Plan sa blokom održavanja traženim kao linearan: blok se upisuje ravan, mezociklus je ravan od četiri nedelje,
    /// vežbe nose propis održavanja (složene 6-10, izolacija 10-20, rezerva 3) i grudi dobijaju MEV od deset serija
    /// nedeljno umesto šesnaest koliko bi dobile u hipertrofiji.
    /// </summary>
    [Fact]
    public async Task ACreatedMaintenanceBlock_IsFlat_AndAimsAtMev()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, ExperienceLevel.Intermediate);

        var maintenance = await CreatePlanAsync(database, Goal.Maintenance, PeriodizationModel.Linear);
        var hypertrophy = await CreatePlanAsync(database, Goal.Hypertrophy, PeriodizationModel.Flat);

        Assert.Equal(PeriodizationModel.Flat, maintenance.Blocks.Single().PeriodizationModel);
        Assert.Equal(4, maintenance.Blocks.Single().DurationWeeks);

        await using var read = database.NewContext();
        var block = await read.Mesocycles.SingleAsync(mesocycle => mesocycle.Id == maintenance.Blocks.Single().MesocycleId);
        Assert.Equal((Goal.Maintenance, PeriodizationModel.Flat), (block.Goal, block.PeriodizationModel));
        Assert.Equal(4, await read.TrainingWeeks.CountAsync(week => week.MesocycleId == block.Id));

        var plans = await read.ExercisePlans
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == block.Id
                           && plan.WorkoutSession.TrainingWeek.WeekNumber == 1)
            .Select(plan => new { plan.Exercise.Type, plan.RepRangeMin, plan.RepRangeMax, plan.TargetRir })
            .ToListAsync();

        Assert.All(plans, plan => Assert.Equal(3, plan.TargetRir));
        Assert.All(
            plans,
            plan => Assert.Equal(
                plan.Type == ExerciseType.Isolation ? (10, 20) : (6, 10),
                (plan.RepRangeMin, plan.RepRangeMax)));

        Assert.Equal(10m, await WeeklyChestSetsAsync(read, block.Id));
        Assert.Equal(16m, await WeeklyChestSetsAsync(read, hypertrophy.Blocks.Single().MesocycleId!.Value));
    }

    /// <summary>
    /// Generator je javan servis i ne sme da veruje da je ko ga zove već ispravio model: jedini pozivalac je danas
    /// servis plana, koji to radi, pa je ovo druga brava, a bez ovog testa njeno vraćanje ne bi pokazalo ništa.
    /// </summary>
    [Fact]
    public async Task TheGenerator_BuildsAMaintenanceBlockFlat_WhateverModelIsAsked()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, ExperienceLevel.Intermediate);

        await using (var db = database.NewContext())
        {
            var generator = new MesocycleGenerator(
                db,
                new WeeklySetPlanner(db, new VolumeLandmarkService(db), new WeeklyVolumeTargetResolver(db)),
                new WorkoutTemplateResolver(db));

            await generator.GenerateAsync(
                database.UserId,
                new StrengthPlanner.Application.DTOs.Mesocycles.GenerateMesocycleRequest
                {
                    Name = "Blok",
                    TemplateKey = "upper-lower",
                    Goal = Goal.Maintenance,
                    PeriodizationModel = PeriodizationModel.Linear,
                    StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        await using var read = database.NewContext();
        var block = await read.Mesocycles.SingleAsync();
        Assert.Equal(PeriodizationModel.Flat, block.PeriodizationModel);
        Assert.Equal(4, await read.TrainingWeeks.CountAsync(week => week.MesocycleId == block.Id));
    }

    [Fact]
    public async Task ASuggestedMaintenancePlan_IsFlatMaintenanceBlocks()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await using var db = database.NewContext();

        var blocks = await new MacrocycleService(db, null!, new WorkoutTemplateResolver(db))
            .SuggestBlocksAsync(database.UserId, 3, Goal.Maintenance, "upper-lower");

        Assert.Equal(3, blocks.Count);
        Assert.All(blocks, block => Assert.Equal((Goal.Maintenance, PeriodizationModel.Flat), (block.Goal, block.PeriodizationModel)));
    }

    // ---- priprema -----------------------------------------------------------------------------------------------

    private static async Task<MacrocycleDto> CreatePlanAsync(SqliteDatabase database, Goal goal, PeriodizationModel model)
    {
        await using var db = database.NewContext();
        var resolver = new WorkoutTemplateResolver(db);
        var generator = new MesocycleGenerator(
            db,
            new WeeklySetPlanner(db, new VolumeLandmarkService(db), new WeeklyVolumeTargetResolver(db)),
            resolver);

        return await new MacrocycleService(db, generator, resolver).CreateAsync(
            database.UserId,
            new CreateMacrocycleRequest
            {
                Name = $"Plan {goal}",
                StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                Blocks =
                [
                    new CreateMacrocycleBlockDto
                    {
                        Goal = goal,
                        TemplateKey = "upper-lower",
                        PeriodizationModel = model,
                        SetAllocation = SetAllocation.TargetVolume
                    }
                ]
            });
    }

    /// <summary>Serije grudi u prvoj nedelji, svaka puta udeo koji vežba daje grudima.</summary>
    private static async Task<decimal> WeeklyChestSetsAsync(AppDbContext read, Guid mesocycleId)
    {
        // Spajanje, ne SelectMany: SQLite ne ume APPLY, a servisi koji se ovde proveravaju ga ne traže.
        var rows = await (
            from plan in read.ExercisePlans
            join muscle in read.Set<ExerciseMuscle>() on plan.ExerciseId equals muscle.ExerciseId
            where plan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                  && plan.WorkoutSession.TrainingWeek.WeekNumber == 1
                  && muscle.MuscleGroup.Name == "Chest"
            select new { plan.TargetSets, muscle.Contribution })
            .ToListAsync();

        return rows.Sum(row => row.TargetSets * row.Contribution);
    }

    private static async Task SeedCatalogAsync(SqliteDatabase database)
    {
        await using var services = new ServiceCollection()
            .AddScoped(_ => database.NewContext())
            .BuildServiceProvider();
        await DbSeeder.SeedAsync(services);
    }

    private static async Task SetLevelAsync(SqliteDatabase database, ExperienceLevel level)
    {
        await using var db = database.NewContext();
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Age = 30,
            BodyweightKg = 80m,
            ExperienceLevel = level
        });
        await db.SaveChangesAsync();
    }

    private static async Task<StrengthPlanner.Application.DTOs.Sessions.CompleteSessionResultDto> CompleteAsync(
        SqliteDatabase database,
        Guid sessionId)
    {
        await using var context = database.NewContext();
        return await SessionCompletionTests.NewService(context)
            .CompleteAsync(database.UserId, sessionId, null, CancellationToken.None);
    }

    private sealed record Seed(Guid Squat, Guid WeekOne, Guid SessionOne, Guid SessionTwo);

    /// <summary>
    /// Blok od četiri nedelje, jedan trening nedeljno, jedan mišić (četvoroglavi, MEV 8, MAV 12, MRV 20) i jedna vežba.
    /// Trening prve nedelje je u toku i ima <paramref name="sets"/> odrađenih serija; druga nedelja je planirana.
    /// </summary>
    private static async Task<Seed> SeedAsync(
        SqliteDatabase database,
        Goal goal,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int sets,
        int reps,
        int rir,
        bool afterDeload = false,
        bool heavyWeek = false)
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
            Goal = goal,
            PeriodizationModel = PeriodizationModel.Flat,
            SetAllocation = SetAllocation.FollowTemplate,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        Guid weekOne = default, sessionOne = default, sessionTwo = default;

        // Raspored: nedelja 1 je u toku, nedelja 2 planirana. Posle rasterećenja: nedelja 1 je odrađena, nedelja 2 je
        // rasterećenje u toku (auto-deload povučen ranije), nedelja 3 je planirana.
        var lastWeekWithSessions = afterDeload ? 3 : 2;
        var weekInProgress = afterDeload ? 2 : 1;

        for (var weekNumber = 1; weekNumber <= 4; weekNumber++)
        {
            var isDeload = afterDeload ? weekNumber is 2 or 4 : weekNumber == 4;
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = isDeload };
            mesocycle.Weeks.Add(week);

            if (weekNumber > lastWeekWithSessions)
            {
                continue;
            }

            var session = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber < weekInProgress
                    ? SessionStatus.Completed
                    : weekNumber == weekInProgress ? SessionStatus.InProgress : SessionStatus.Planned
            };
            var plan = new ExercisePlan
            {
                Id = Guid.NewGuid(),
                ExerciseId = squat,
                Order = 1,
                TargetSets = 3,
                PrescribedSets = 3,
                RepRangeMin = repRangeMin,
                RepRangeMax = repRangeMax,
                BaseRepRangeMin = repRangeMin,
                BaseRepRangeMax = repRangeMax,
                TargetRir = targetRir,
                TargetWeightKg = 100m
            };

            if (weekNumber <= weekInProgress)
            {
                // Odrađena nedelja i nedelja u toku nose iste serije; rasterećenje u toku bi nosilo lakše, ali
                // progresija se posle njega čita iz odrađene trenažne nedelje, ne iz njega.
                for (var number = 1; number <= sets; number++)
                {
                    // Teška nedelja: prve dve serije su teške, a poslednje dve do otkaza ispod opsega (isto kao u
                    // BlockEditServiceTests.CompleteHeavyWeekFourAsync, 0.35 + 0.25 = prag srednjeg nivoa).
                    var toFailure = heavyWeek ? number > sets / 2 : !isDeload && rir == 0;

                    plan.SetLogs.Add(new SetLog
                    {
                        Id = Guid.NewGuid(),
                        SetNumber = number,
                        WeightKg = isDeload ? 90m : 100m,
                        Reps = heavyWeek && toFailure ? Math.Max(repRangeMin - 2, 1) : reps,
                        Rir = isDeload ? Math.Min(rir + 2, 5) : rir,
                        IsFailure = toFailure,
                        PerformedAt = DateTime.UtcNow
                    });
                }
            }

            if (weekNumber == weekInProgress)
            {
                weekOne = week.Id;
                sessionOne = session.Id;
            }
            else if (weekNumber == weekInProgress + 1)
            {
                sessionTwo = session.Id;
            }

            session.ExercisePlans.Add(plan);
            week.Sessions.Add(session);
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new Seed(squat, weekOne, sessionOne, sessionTwo);
    }
}
