namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// When a finished workout happened, for ordering and for the screen.
///
/// Workouts finished before the completion time was stored have only their sets to say when they
/// were done, and a skipped one has not even that, so the date comes from the best source there
/// is, and says whether it is a real one.
/// </summary>
public static class HistoryDate
{
    public readonly record struct WorkoutDate(DateTime Value, bool Known);

    /// <summary>
    /// In order of trust: the completion time, the last set, a date the plan gave the workout
    /// (a promise, not a fact), and last the week of the block it was planned for.
    ///
    /// A date taken from the plan is never later than <paramref name="now"/>. A workout in the
    /// history is over, and a skip is something that already happened, so a week that lies ahead
    /// would put it above everything that was actually done.
    /// </summary>
    public static WorkoutDate Of(
        DateTime? completedAt,
        DateTime? lastSetAt,
        DateTime? plannedDate,
        DateTime blockStart,
        int weekNumber,
        DateTime now)
    {
        if (completedAt is not null)
        {
            return new WorkoutDate(completedAt.Value, true);
        }

        if (lastSetAt is not null)
        {
            return new WorkoutDate(lastSetAt.Value, true);
        }

        if (plannedDate is not null)
        {
            return new WorkoutDate(Min(plannedDate.Value, now), false);
        }

        return new WorkoutDate(Min(blockStart.AddDays(7 * (weekNumber - 1)), now), false);
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;
}
