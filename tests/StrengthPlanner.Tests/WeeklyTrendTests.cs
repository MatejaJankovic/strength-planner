using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Tonnage and weekly volume used to be read one block at a time. The trend groups the sets by
/// calendar week across blocks, and says which block a week belonged to, whether it was a deload
/// and where a new block began, so a drop at a boundary is not read as lost strength.
/// </summary>
public class WeeklyTrendTests
{
    // 2026-11-23 is a Monday.
    private static readonly DateTime Monday = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Monday.AddDays(2).AddHours(10);

    private static readonly Guid BlockA = Guid.NewGuid();
    private static readonly Guid BlockB = Guid.NewGuid();
    private static readonly Guid Chest = Guid.NewGuid();
    private static readonly Guid Triceps = Guid.NewGuid();

    private static TrendSet Set(
        DateTime at,
        decimal loadKg = 100m,
        int reps = 5,
        int rir = 2,
        bool failure = false,
        Guid? block = null,
        bool deload = false,
        params TrendMuscle[] muscles)
    {
        return new TrendSet(
            at,
            loadKg,
            reps,
            rir,
            failure,
            block ?? BlockA,
            block == BlockB ? "Blok B" : "Blok A",
            "Plan",
            deload,
            muscles.Length > 0 ? muscles : [new TrendMuscle(Chest, 1m)]);
    }

    [Theory]
    [InlineData(2026, 11, 23, 0, 0, 23)]
    [InlineData(2026, 11, 29, 23, 59, 23)]
    [InlineData(2026, 11, 30, 0, 0, 30)]
    [InlineData(2026, 11, 26, 12, 0, 23)]
    public void AWeek_StartsOnMondayAtMidnight(int year, int month, int day, int hour, int minute, int expectedDay)
    {
        var start = WeeklyTrend.WeekStartOf(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 11, expectedDay, 0, 0, 0, DateTimeKind.Utc), start);
    }

    [Fact]
    public void WithNoSets_ThereIsNothingToShow_WhenAllHistoryIsAsked()
    {
        Assert.Empty(WeeklyTrend.Build([], Now, weeks: null));
    }

    /// <summary>
    /// Once training has begun, the weeks after it are kept even when empty: a gap, or a week of
    /// rest, is information. The range runs up to the current week.
    /// </summary>
    [Fact]
    public void AGapAfterTheFirstSet_IsKept_UpToTheCurrentWeek()
    {
        var weeks = WeeklyTrend.Build(
            [Set(Monday.AddDays(-21).AddHours(9)), Set(Monday.AddDays(-7).AddHours(9))],
            Now,
            weeks: 6);

        Assert.Equal([1, 0, 1, 0], weeks.Select(week => week.Sets));
        Assert.Equal(Monday.AddDays(-21), weeks[0].WeekStart);
        Assert.Equal(Monday, weeks[3].WeekStart);
        Assert.Null(weeks[1].BlockId);
    }

    /// <summary>
    /// The weeks before the first set in the range say nothing, and a range wider than the history
    /// would open with a screenful of "no training". A fixed range is at most that many weeks,
    /// starting no earlier than the first set.
    /// </summary>
    [Fact]
    public void WeeksBeforeTheFirstSet_AreNotShown_EvenInAFixedRange()
    {
        var weeks = WeeklyTrend.Build([Set(Monday.AddDays(-14).AddHours(9))], Now, weeks: 26);

        Assert.Equal(3, weeks.Count);
        Assert.Equal(Monday.AddDays(-14), weeks[0].WeekStart);
    }

    [Fact]
    public void ARangeWithNoTrainingInIt_IsEmpty_NotAScreenOfEmptyWeeks()
    {
        Assert.Empty(WeeklyTrend.Build([Set(Monday.AddDays(-100))], Now, weeks: 4));
    }

    [Fact]
    public void AllHistory_ReachesBackToTheFirstSet()
    {
        var weeks = WeeklyTrend.Build(
            [Set(Monday.AddDays(-30)), Set(Monday.AddDays(1))],
            Now,
            weeks: null);

        // The first set was 30 days before a Monday: the Monday five weeks before this one.
        Assert.Equal(Monday.AddDays(-35), weeks[0].WeekStart);
        Assert.Equal(Monday, weeks[^1].WeekStart);
        Assert.Equal(6, weeks.Count);
    }

    [Fact]
    public void SetsBeforeTheRange_AreLeftOut()
    {
        var weeks = WeeklyTrend.Build(
            [Set(Monday.AddDays(-30)), Set(Monday.AddDays(1))],
            Now,
            weeks: 2);

        // Two weeks back, but the older set is outside them, and the week before the first set
        // inside is not shown: what is left is the current week.
        Assert.Single(weeks);
        Assert.Equal(Monday, weeks[0].WeekStart);
        Assert.Equal(1, weeks.Sum(week => week.Sets));
    }

    [Fact]
    public void ASetFromAWeekThatHasNotBegun_IsNotTrainingThatHappened()
    {
        var weeks = WeeklyTrend.Build([Set(Monday.AddDays(8)), Set(Monday.AddDays(1))], Now, weeks: null);

        Assert.Equal(1, weeks.Sum(week => week.Sets));
    }

    /// <summary>Tonnage is weight times reps, with the body mass the movement carries.</summary>
    [Fact]
    public void Tonnage_IsTotalLoadTimesReps()
    {
        var weeks = WeeklyTrend.Build(
            [
                Set(Monday.AddHours(8), loadKg: 100m, reps: 5),
                Set(Monday.AddHours(9), loadKg: 85m, reps: 6)
            ],
            Now,
            weeks: 1);

        Assert.Equal(100m * 5 + 85m * 6, weeks[0].TonnageKg);
    }

    /// <summary>
    /// Stimulative sets follow the same credit as the weekly volume screen: a set to failure or
    /// within three reps of it counts whole, four in reserve counts half, five counts nothing. A
    /// muscle takes its share of each set.
    /// </summary>
    [Fact]
    public void StimulativeSets_UseTheSameCreditAsTheVolumeScreen_AndSplitByMuscle()
    {
        var presses = new[] { new TrendMuscle(Chest, 1m), new TrendMuscle(Triceps, 0.5m) };
        var weeks = WeeklyTrend.Build(
            [
                Set(Monday.AddHours(8), rir: 1, muscles: presses),
                Set(Monday.AddHours(9), rir: 4, muscles: presses),
                Set(Monday.AddHours(10), rir: 5, muscles: presses),
                Set(Monday.AddHours(11), rir: 3, failure: true, muscles: presses)
            ],
            Now,
            weeks: 1);

        // 1 + 0.5 + 0 + 1 over four sets.
        Assert.Equal(2.5m, weeks[0].StimulativeSets);
        Assert.Equal(2.5m, weeks[0].StimulativeSetsByMuscle[Chest]);
        Assert.Equal(1.25m, weeks[0].StimulativeSetsByMuscle[Triceps]);
        Assert.Equal(4, weeks[0].Sets);
    }

    /// <summary>
    /// A week belongs to the block with most of its sets; the first week a block appears is where
    /// it starts, and the weeks after it are not new starts.
    /// </summary>
    [Fact]
    public void AWeek_BelongsToItsMainBlock_AndTheBoundaryIsMarkedOnce()
    {
        var weeks = WeeklyTrend.Build(
            [
                Set(Monday.AddDays(-14).AddHours(8), block: BlockA),
                Set(Monday.AddDays(-7).AddHours(8), block: BlockA),
                Set(Monday.AddHours(8), block: BlockB),
                Set(Monday.AddHours(9), block: BlockB)
            ],
            Now,
            weeks: 3);

        Assert.Equal([BlockA, BlockA, BlockB], weeks.Select(week => week.BlockId));
        Assert.Equal([true, false, true], weeks.Select(week => week.BlockStarts));
        Assert.Equal("Blok B", weeks[2].BlockName);
    }

    [Fact]
    public void AWeekOfBothBlocks_GoesToTheOneWithMoreSets_AndOnATieToTheOneTrainedLater()
    {
        var more = WeeklyTrend.Build(
            [
                Set(Monday.AddHours(8), block: BlockA),
                Set(Monday.AddHours(9), block: BlockA),
                Set(Monday.AddDays(1), block: BlockB)
            ],
            Now,
            weeks: 1);
        var tie = WeeklyTrend.Build(
            [Set(Monday.AddHours(8), block: BlockA), Set(Monday.AddDays(1), block: BlockB)],
            Now,
            weeks: 1);

        Assert.Equal(BlockA, more[0].BlockId);
        Assert.Equal(BlockB, tie[0].BlockId);
    }

    /// <summary>A week is a deload when at least half its sets were done in a deload week.</summary>
    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(1, 2, true)]
    [InlineData(1, 3, false)]
    [InlineData(0, 2, false)]
    public void ADeloadWeek_IsOneWhereHalfOfTheSetsWereDeloadSets(int deloadSets, int totalSets, bool expected)
    {
        var sets = Enumerable.Range(0, totalSets)
            .Select(index => Set(Monday.AddHours(8 + index), deload: index < deloadSets))
            .ToList();

        var weeks = WeeklyTrend.Build(sets, Now, weeks: 1);

        Assert.Equal(expected, weeks[0].IsDeload);
    }

    /// <summary>
    /// Switching block with a gap between is still a boundary, and the same block coming back after
    /// an empty week is not a new start.
    /// </summary>
    [Fact]
    public void AnEmptyWeek_DoesNotBreakABlock_IntoTwo()
    {
        var weeks = WeeklyTrend.Build(
            [
                Set(Monday.AddDays(-14).AddHours(8), block: BlockA),
                Set(Monday.AddHours(8), block: BlockA)
            ],
            Now,
            weeks: 3);

        Assert.Equal([true, false, false], weeks.Select(week => week.BlockStarts));
        Assert.Null(weeks[1].BlockId);
    }
}
