using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.Templates;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Šta pravi generator, sa pravim katalogom vežbi i pravim šablonima. Račun propisa vežbe je u domenu
/// (<c>ExercisePrescriptions</c>) i testiran je tamo, ali ništa nije proveravalo da ga generator zove
/// sa pravim ulazima: izmereno, kad generator zaboravi tip vežbe, serije iz ličnog šablona ili nivo bloka,
/// 0 od 1627 testova pada. Isto što i kod <c>SetLogDto</c> (runda 9) i izolacije (runda 10): pravilo
/// postoji, a primena nigde nije pod proverom.
///
/// Brojevi ovde su upisani, a ne izračunati istom funkcijom koju proveravaju: test koji očekuje ono što kod
/// računa ne bi opazio da kod računa pogrešno.
/// </summary>
public class MesocycleGeneratorPrescriptionTests
{
    [Fact]
    public async Task TheRepRange_FollowsTheTypeOfTheExercise_InAStrengthBlock()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, ExperienceLevel.Intermediate);

        var plans = await GenerateWeekOneAsync(database, "upper-lower", Goal.Strength);

        // Izolacija 10-20, složena vežba koja podnosi nizak opseg 3-6, a složena koja ga ne podnosi 8-12.
        Assert.All(plans, plan =>
        {
            var expected = plan.Type == ExerciseType.Isolation ? (10, 20) : plan.SuitsLowReps ? (3, 6) : (8, 12);

            Assert.Equal(expected, (plan.BaseRepRangeMin, plan.BaseRepRangeMax));
            Assert.Equal(expected, (plan.RepRangeMin, plan.RepRangeMax));
        });

        // Da provera nije prazna: blok ima obe vrste vežbi.
        Assert.Contains(plans, plan => plan.Type == ExerciseType.Isolation);
        Assert.Contains(plans, plan => plan.Type == ExerciseType.Compound && plan.SuitsLowReps);
    }

    [Fact]
    public async Task TheRepRange_OfAHypertrophyBlock_IsTheSameForCompoundsAndHigherForIsolations()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, ExperienceLevel.Intermediate);

        var plans = await GenerateWeekOneAsync(database, "upper-lower", Goal.Hypertrophy);

        Assert.All(plans, plan =>
            Assert.Equal(
                plan.Type == ExerciseType.Isolation ? (10, 20) : (8, 12),
                (plan.BaseRepRangeMin, plan.BaseRepRangeMax)));
        Assert.All(plans, plan => Assert.Equal(1, plan.TargetRir));
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 3)]
    [InlineData(ExperienceLevel.Intermediate, 4)]
    [InlineData(ExperienceLevel.Advanced, 4)]
    public async Task TheStartingSets_FollowTheLevelOfTheLifter(ExperienceLevel level, int expectedSets)
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, level);

        var plans = await GenerateWeekOneAsync(database, "upper-lower", Goal.Hypertrophy);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            Assert.Equal(expectedSets, plan.PrescribedSets);
            Assert.Equal(expectedSets, plan.TargetSets);
        });
    }

    /// <summary>
    /// Lični šablon nosi svoje serije i opseg, i oni su propis prve nedelje: ne zamenjuju ih ni nivo ni
    /// cilj bloka.
    /// </summary>
    [Fact]
    public async Task ACustomTemplate_KeepsItsOwnSetsAndRepRanges()
    {
        using var database = new SqliteDatabase();
        await SeedCatalogAsync(database);
        await SetLevelAsync(database, ExperienceLevel.Intermediate);
        var templateKey = await AddCustomTemplateAsync(database);

        var plans = await GenerateWeekOneAsync(database, templateKey, Goal.Hypertrophy);

        var bench = plans.Single(plan => plan.Name == "Bench Press");
        var raise = plans.Single(plan => plan.Name == "Lateral Raise");

        Assert.Equal((6, 5, 5), (bench.PrescribedSets, bench.BaseRepRangeMin, bench.BaseRepRangeMax));
        Assert.Equal((5, 5), (bench.RepRangeMin, bench.RepRangeMax));
        Assert.Equal((2, 12, 15), (raise.PrescribedSets, raise.BaseRepRangeMin, raise.BaseRepRangeMax));
        Assert.Equal((12, 15), (raise.RepRangeMin, raise.RepRangeMax));
    }

    // ---- priprema ------------------------------------------------------------------------

    private static async Task SeedCatalogAsync(SqliteDatabase database)
    {
        // Pravi seeder, pravi katalog: šabloni vežbe traže po nazivu.
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

    private static async Task<string> AddCustomTemplateAsync(SqliteDatabase database)
    {
        await using var db = database.NewContext();
        var bench = await db.Exercises.SingleAsync(exercise => exercise.Name == "Bench Press");
        var raise = await db.Exercises.SingleAsync(exercise => exercise.Name == "Lateral Raise");

        var template = new UserWorkoutTemplate
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Moj šablon",
            CreatedAt = DateTime.UtcNow,
            Days =
            [
                new UserWorkoutTemplateDay
                {
                    Id = Guid.NewGuid(),
                    Name = "Dan 1",
                    Order = 1,
                    Exercises =
                    [
                        new UserWorkoutTemplateExercise
                        {
                            Id = Guid.NewGuid(), ExerciseId = bench.Id, Order = 1, Sets = 6, RepRangeMin = 5, RepRangeMax = 5
                        },
                        new UserWorkoutTemplateExercise
                        {
                            Id = Guid.NewGuid(), ExerciseId = raise.Id, Order = 2, Sets = 2, RepRangeMin = 12, RepRangeMax = 15
                        }
                    ]
                }
            ]
        };
        db.UserWorkoutTemplates.Add(template);
        await db.SaveChangesAsync();

        return CustomTemplateKey.For(template.Id);
    }

    /// <summary>
    /// Generiše blok pravim generatorom i vraća planove prve nedelje. Ravan model, pa prva nedelja nosi
    /// propis bez pomeraja, a „prati moj šablon" ne dira broj serija balansiranjem.
    /// </summary>
    private static async Task<List<PlanRow>> GenerateWeekOneAsync(SqliteDatabase database, string templateKey, Goal goal)
    {
        await using (var db = database.NewContext())
        {
            var generator = new MesocycleGenerator(
                db,
                new WeeklySetPlanner(db, new VolumeLandmarkService(db), new WeeklyVolumeTargetResolver(db)),
                new WorkoutTemplateResolver(db));

            await generator.GenerateAsync(
                database.UserId,
                new GenerateMesocycleRequest
                {
                    Name = "Blok",
                    TemplateKey = templateKey,
                    Goal = goal,
                    PeriodizationModel = PeriodizationModel.Flat,
                    SetAllocation = SetAllocation.FollowTemplate,
                    StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        await using var read = database.NewContext();
        return await read.ExercisePlans
            .Where(plan => plan.WorkoutSession.TrainingWeek.WeekNumber == 1)
            .Select(plan => new PlanRow(
                plan.Exercise.Name,
                plan.Exercise.Type,
                plan.Exercise.SuitsLowReps,
                plan.PrescribedSets,
                plan.TargetSets,
                plan.RepRangeMin,
                plan.RepRangeMax,
                plan.BaseRepRangeMin,
                plan.BaseRepRangeMax,
                plan.TargetRir))
            .ToListAsync();
    }

    private sealed record PlanRow(
        string Name,
        ExerciseType Type,
        bool SuitsLowReps,
        int PrescribedSets,
        int TargetSets,
        int RepRangeMin,
        int RepRangeMax,
        int BaseRepRangeMin,
        int BaseRepRangeMax,
        int TargetRir);
}
