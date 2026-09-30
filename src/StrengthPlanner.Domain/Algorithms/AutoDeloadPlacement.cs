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
    /// A block carries one deload: pulling it forward releases the planned one at the end
    /// for exactly that reason. A second auto-deload broke the promise, and in one case it
    /// also crashed: when the first had taken the base week, a second one landing on the
    /// released week could no longer read the block's base sets.
    ///
    /// The cost, stated plainly: fatigue after week 1 moves the deload to week 2, and weeks
    /// 3 to 6 then run without one - four training weeks, fewer than the five a periodized
    /// block carries before its planned deload. Later weeks are still scored, and the volume
    /// limits still learn from them (MRV reads reserve, failures and a real decline), so the
    /// fatigue is not ignored - it only no longer buys a second deload inside the same block.
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
