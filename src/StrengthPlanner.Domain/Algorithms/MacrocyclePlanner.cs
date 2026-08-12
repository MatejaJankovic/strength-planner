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

    /// <summary>Most blocks a plan can hold; six four-week blocks is already half a year.</summary>
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
}
