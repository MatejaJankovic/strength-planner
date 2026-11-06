using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Blok se sudi po nivou sa kojim je generisan, ne po trenutnom profilu (#74). Promena nivoa
/// usred bloka je pomerala cilj grudi sa 16 na 19 serija i prag deload-a sa 0.60 na 0.50, a
/// vraćanje te ispravke je ostavljalo ceo paket zelen: 0 od 613.
/// </summary>
public class BlockExperienceLevelTests
{
    [Fact]
    public async Task ALevelChangedMidBlock_DoesNotReachTheRunningBlock()
    {
        using var database = new SqliteDatabase();
        var mesocycleId = Guid.NewGuid();

        await using (var db = database.NewContext())
        {
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
        }

        await using var context = database.NewContext();
        var level = await BlockExperienceLevel.ForBlockAsync(context, database.UserId, mesocycleId, CancellationToken.None);

        Assert.Equal(ExperienceLevel.Intermediate, level);
    }
}
