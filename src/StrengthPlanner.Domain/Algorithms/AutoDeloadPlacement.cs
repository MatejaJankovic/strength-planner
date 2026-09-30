namespace StrengthPlanner.Domain.Algorithms;

/// <summary>A week of a block, as the auto-deload sees it.</summary>
public sealed record BlockWeekState(int WeekNumber, bool IsDeload, bool IsAutoDeload, bool HasStarted);

/// <summary>
/// Which week fatigue may turn into a deload, if any.
/// </summary>
public static class AutoDeloadPlacement
{
    /// <summary>
    /// The week after the evaluated one, when it exists, is not already a deload and has not
    /// started - and only while the block has not pulled a deload forward yet.
    ///
    /// A block carries one deload. The first auto-deload releases the planned one at the end,
    /// so a second would leave the block with two, and the week it would land on is exactly
    /// that released week: from there nothing is left to restore, and when the first
    /// auto-deload took the base week, the block's base sets can no longer be read either.
    /// A lifter who is fatigued again after resting once is at the end of the block by then;
    /// the next block starts from a fresh prescription.
    /// </summary>
    public static int? NextWeek(IReadOnlyCollection<BlockWeekState> weeks, int evaluatedWeekNumber)
    {
        if (weeks.Any(week => week.IsAutoDeload))
        {
            return null;
        }

        // Deload se sme staviti samo na nedelju koja još nije počela: prepisivanje ciljeva
        // započetog treninga bi falsifikovalo istoriju.
        var next = weeks.FirstOrDefault(week => week.WeekNumber == evaluatedWeekNumber + 1);

        return next is null || next.IsDeload || next.HasStarted ? null : next.WeekNumber;
    }
}
