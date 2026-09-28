using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Which experience level a block is judged by.
///
/// The level decides two things that are read again after every completed week: the
/// scaled volume landmarks and the auto-deload threshold. Both used to read the profile,
/// so changing the level rewrote a block that was already running — the guide promises
/// the opposite, and a plan should not move under the lifter's hands.
///
/// The answer therefore comes from the block, which stored it when it was generated.
/// Two readers ask, so the rule lives in one place, like
/// <see cref="ComparableWeek"/>.
/// </summary>
public static class BlockExperienceLevel
{
    /// <summary>
    /// The level the block was generated with.
    ///
    /// When the block row is gone — it can only be a delete racing this read — the
    /// profile answers instead. That is exactly what every caller did before the level
    /// was stored, so the fallback cannot be newly wrong; it is not a training decision.
    /// </summary>
    public static async Task<ExperienceLevel> ForBlockAsync(
        AppDbContext db,
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var stored = await db.Mesocycles
            .AsNoTracking()
            .Where(mesocycle => mesocycle.Id == mesocycleId && mesocycle.UserId == userId)
            .Select(mesocycle => (ExperienceLevel?)mesocycle.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is not null)
        {
            return stored.Value;
        }

        return await db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;
    }
}
