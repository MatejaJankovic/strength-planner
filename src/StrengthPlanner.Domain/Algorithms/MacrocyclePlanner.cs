using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Builds the default block sequence for a long-term plan.
///
/// Alternating hypertrophy and strength is the standard way to chain blocks: a
/// hypertrophy block adds tissue at moderate loads, and the strength block that follows
/// teaches the lifter to express it at heavy ones. Running either kind indefinitely
/// gives up half of that — which is exactly what a system that only ever plans one
/// mesocycle at a time forces on the user.
/// </summary>
public static class MacrocyclePlanner
{
    /// <summary>Fewest blocks a long-term plan can hold — one block is a plain mesocycle.</summary>
    public const int MinBlocks = 1;

    /// <summary>Most blocks a plan can hold; six blocks run half a year to nine months.</summary>
    public const int MaxBlocks = 6;

    /// <summary>
    /// Returns the goals for <paramref name="blockCount"/> blocks, alternating from
    /// <paramref name="firstGoal"/>.
    /// </summary>
    public static IReadOnlyList<Goal> AlternatingGoals(int blockCount, Goal firstGoal)
    {
        if (blockCount < MinBlocks || blockCount > MaxBlocks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockCount),
                $"A plan holds between {MinBlocks} and {MaxBlocks} blocks.");
        }

        var other = firstGoal == Goal.Hypertrophy ? Goal.Strength : Goal.Hypertrophy;

        return Enumerable
            .Range(0, blockCount)
            .Select(index => index % 2 == 0 ? firstGoal : other)
            .ToList();
    }

    /// <summary>True when the block count is inside the supported range.</summary>
    public static bool IsValidBlockCount(int blockCount)
    {
        return blockCount is >= MinBlocks and <= MaxBlocks;
    }

    /// <summary>
    /// The day the next block of a plan starts on.
    ///
    /// It used to be the day after the last session of the previous block. That kept a plan
    /// from starting in the past, but it also moved every block's week off the grid its own
    /// week shape assumes, so the spacing a template keeps inside a week was lost at the
    /// seam: Legs Specialization ends on Legs C (Saturday) and starts on Legs A, which then
    /// fell on Sunday - legs on two days in a row, at every level. Found in review.
    ///
    /// So the block starts where the previous one's weeks end - its start plus its weeks -
    /// and only later if that would be on or before the last session, or in the past: a
    /// block stretched past its planned end must not get a plan that is already late.
    /// </summary>
    public static DateTime NextBlockStart(
        DateTime previousStart,
        int previousDurationWeeks,
        DateTime? lastSessionDate,
        DateTime today)
    {
        var plannedEnd = previousStart.Date.AddDays(previousDurationWeeks * 7);
        var afterLastSession = (lastSessionDate ?? today).Date.AddDays(1);

        var start = plannedEnd > afterLastSession ? plannedEnd : afterLastSession;

        return start > today.Date ? start : today.Date;
    }
}
