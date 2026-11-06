using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Blok se sudi po nivou sa kojim je generisan, ne po trenutnom profilu (#74). Promena nivoa
/// usred bloka je pomerala cilj grudi sa 16 na 19 serija i prag deload-a sa 0.60 na 0.50.
///
/// Ovde su pokriveni pomoćna klasa i jedan od dva pozivaoca, `VolumeLandmarkService`. Drugi
/// pozivalac (`DeloadService`) i upis nivoa u generatoru i dalje nemaju test: vraćanje svakog
/// od njih ostavlja paket zelen (izmereno u pregledu #104).
/// </summary>
public class BlockExperienceLevelTests
{
    [Fact]
    public async Task ALevelChangedMidBlock_DoesNotReachTheRunningBlock()
    {
        using var database = new SqliteDatabase();
        var mesocycleId = await SeedAsync(database);

        await using var context = database.NewContext();
        var level = await BlockExperienceLevel.ForBlockAsync(context, database.UserId, mesocycleId, CancellationToken.None);

        Assert.Equal(ExperienceLevel.Intermediate, level);
    }

    /// <summary>
    /// Pozivalac: granice volumena bloka srednjeg nivoa ostaju grudi 10 / 16 / 22, iako je
    /// profil u međuvremenu napredan (×1.2 bi dalo cilj 19).
    /// </summary>
    [Fact]
    public async Task TheVolumeLimits_OfARunningBlock_KeepItsLevel()
    {
        using var database = new SqliteDatabase();
        var mesocycleId = await SeedAsync(database);
        var chest = Guid.NewGuid();

        await using (var db = database.NewContext())
        {
            db.MuscleGroups.Add(new MuscleGroup { Id = chest, Name = "Chest" });
            db.VolumeLandmarks.Add(new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = chest, Mev = 10, Mav = 16, Mrv = 22 });
            await db.SaveChangesAsync();
        }

        await using var context = database.NewContext();
        var limits = await new VolumeLandmarkService(context).GetEffectiveAsync(database.UserId, mesocycleId, CancellationToken.None);

        Assert.Equal((10, 16, 22), (limits[chest].Mev, limits[chest].Mav, limits[chest].Mrv));
    }

    private static async Task<Guid> SeedAsync(SqliteDatabase database)
    {
        var mesocycleId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Age = 30,
            BodyweightKg = 80m,
            ExperienceLevel = ExperienceLevel.Advanced
        });
        db.Mesocycles.Add(new Mesocycle
        {
            Id = mesocycleId,
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Hypertrophy,
            ExperienceLevel = ExperienceLevel.Intermediate,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });
        await db.SaveChangesAsync();
        return mesocycleId;
    }
}
