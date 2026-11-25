using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// The history orders workouts by when they happened. Workouts finished before the completion
/// time was stored, and skipped ones, have to be placed by the best date there is, and the screen
/// has to know which dates are real.
/// </summary>
public class HistoryDateTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Completed = new(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime LastSet = new(2026, 11, 10, 18, 5, 0, DateTimeKind.Utc);
    private static readonly DateTime Planned = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheCompletionTime_WinsWhenItIsThere()
    {
        var date = HistoryDate.Of(Completed, LastSet, Planned, Start, weekNumber: 2, Now);

        Assert.Equal(Completed, date.Value);
        Assert.True(date.Known);
    }

    [Fact]
    public void AnOlderWorkout_IsPlacedAtItsLastSet()
    {
        var date = HistoryDate.Of(null, LastSet, Planned, Start, weekNumber: 2, Now);

        Assert.Equal(LastSet, date.Value);
        Assert.True(date.Known);
    }

    /// <summary>
    /// A skipped workout has no sets. The date the plan gave it is a promise and not a fact, so it
    /// is used and said not to be known.
    /// </summary>
    [Fact]
    public void ASkippedWorkout_FallsBackToThePlannedDate_AndSaysItIsNotKnown()
    {
        var date = HistoryDate.Of(null, null, Planned, Start, weekNumber: 2, Now);

        Assert.Equal(Planned, date.Value);
        Assert.False(date.Known);
    }

    /// <summary>
    /// A skip is something that already happened, so a date taken from the plan cannot lie ahead of
    /// now: it would put the skipped workout above everything that was actually done.
    /// </summary>
    [Fact]
    public void ADateFromThePlan_IsNeverInTheFuture()
    {
        var future = Now.AddDays(6);

        var planned = HistoryDate.Of(null, null, future, Start, weekNumber: 2, Now);
        var weekOfTheBlock = HistoryDate.Of(null, null, null, Now.AddDays(3), weekNumber: 1, Now);

        Assert.Equal(Now, planned.Value);
        Assert.Equal(Now, weekOfTheBlock.Value);
        Assert.False(planned.Known);
        Assert.False(weekOfTheBlock.Known);
    }

    /// <summary>A real date is never touched, whatever it is: only a guessed one is limited.</summary>
    [Fact]
    public void ARealDate_IsKeptAsItIs()
    {
        var date = HistoryDate.Of(Now.AddDays(1), null, null, Start, weekNumber: 1, Now);

        Assert.Equal(Now.AddDays(1), date.Value);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 7)]
    [InlineData(4, 21)]
    public void WithNoDateAtAll_TheWeekOfTheBlockStandsIn(int weekNumber, int daysAfterStart)
    {
        var date = HistoryDate.Of(null, null, null, Start, weekNumber, Now);

        Assert.Equal(Start.AddDays(daysAfterStart), date.Value);
        Assert.False(date.Known);
    }
}
