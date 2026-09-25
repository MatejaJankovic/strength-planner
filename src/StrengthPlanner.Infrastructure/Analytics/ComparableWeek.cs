using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Which earlier week a completed one may be measured against.
///
/// Two readers ask this: the fatigue score, for the week as a whole, and the volume limits,
/// per muscle group. They must not answer it differently — a comparison that is fair for
/// one is fair for the other, and a rule copied into two places is a rule that drifts.
/// </summary>
public static class ComparableWeek
{
    /// <summary>
    /// The most recent week before <paramref name="weekNumber"/> that was <b>not</b> a
    /// deload, or null when there is none.
    ///
    /// Deload weeks are excluded because their sets are deliberately submaximal: comparing
    /// against one would make the week after a deload look like a jump, and the deload
    /// itself like a collapse.
    /// </summary>
    public static async Task<int?> PreviousTrainingWeekAsync(
        AppDbContext db,
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (weekNumber <= 1)
        {
            return null;
        }

        return await db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && week.WeekNumber < weekNumber
                           && !week.IsDeload)
            .OrderByDescending(week => week.WeekNumber)
            .Select(week => (int?)week.WeekNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
