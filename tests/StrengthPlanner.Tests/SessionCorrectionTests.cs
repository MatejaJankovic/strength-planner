using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// A finished workout can be corrected while nothing has been built on its sets: the same day
/// next week has not started and no block was generated from them.
/// </summary>
public class SessionCorrectionTests
{
    [Theory]
    [InlineData(SessionStatus.Planned)]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Skipped)]
    public void OnlyACompletedWorkout_IsCorrected(SessionStatus status)
    {
        var window = SessionCorrection.Evaluate(status, true, NextWorkoutState.NotStarted, false);

        Assert.False(window.IsOpen);
        Assert.Equal(CorrectionBlock.NotCompleted, window.BlockedBy);
    }

    [Fact]
    public void ItIsOpen_WhileTheNextWorkoutOfTheSameDayHasNotStarted()
    {
        var window = SessionCorrection.Evaluate(SessionStatus.Completed, true, NextWorkoutState.NotStarted, false);

        Assert.True(window.IsOpen);
        Assert.Equal(CorrectionMode.Recalculate, window.Mode);
        Assert.Equal(CorrectionBlock.None, window.BlockedBy);
    }

    [Fact]
    public void ItCloses_WhenTheNextWorkoutOfTheSameDayWasStarted()
    {
        var window = SessionCorrection.Evaluate(SessionStatus.Completed, true, NextWorkoutState.Started, false);

        Assert.False(window.IsOpen);
        Assert.Equal(CorrectionBlock.NextWorkoutStarted, window.BlockedBy);
    }

    /// <summary>
    /// The last week of a block has no later workout to hand a target to. What its sets fed is
    /// the next block, so the window shuts when that block has been generated and not before.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void WithNoLaterWeek_ItClosesWithTheNextBlock(bool nextBlockOpened, bool expectedOpen)
    {
        var window = SessionCorrection.Evaluate(SessionStatus.Completed, true, NextWorkoutState.None, nextBlockOpened);

        Assert.Equal(expectedOpen, window.IsOpen);
        Assert.Equal(
            expectedOpen ? CorrectionBlock.None : CorrectionBlock.NextBlockOpened,
            window.BlockedBy);
    }

    /// <summary>
    /// A block is followed once its last workout is settled, and a skipped workout is settled.
    /// A planned later workout therefore means no successor can exist yet, and a skipped one
    /// already counts as started, so the case of an opened block with an open window cannot be
    /// reached through the statuses. If it ever were, the later workout still decides.
    /// </summary>
    [Fact]
    public void AnOpenedNextBlock_DoesNotCloseAWorkoutThatStillHasAPlannedLaterWeek()
    {
        var window = SessionCorrection.Evaluate(SessionStatus.Completed, true, NextWorkoutState.NotStarted, true);

        Assert.True(window.IsOpen);
    }

    /// <summary>
    /// Skipping the same day of the next week settles that week, so the week can be scored from
    /// the sets being corrected and, in the last week, the next block is generated from their
    /// estimates. Treating it as not started left the window open after both.
    /// </summary>
    [Fact]
    public void ASkippedNextWorkout_ClosesTheWindow()
    {
        var next = SessionCorrection.NextWorkoutOf(SessionStatus.Skipped);

        var window = SessionCorrection.Evaluate(SessionStatus.Completed, true, next, false);

        Assert.False(window.IsOpen);
        Assert.Equal(CorrectionBlock.NextWorkoutStarted, window.BlockedBy);
    }

    /// <summary>
    /// Workouts completed before the consequences were recorded per workout are corrected as
    /// history: the window follows the same rule, but nothing is recalculated.
    /// </summary>
    [Fact]
    public void AWorkoutWithoutRecordedConsequences_IsCorrectedAsHistoryOnly()
    {
        var window = SessionCorrection.Evaluate(SessionStatus.Completed, false, NextWorkoutState.NotStarted, false);

        Assert.True(window.IsOpen);
        Assert.Equal(CorrectionMode.HistoryOnly, window.Mode);
    }

    [Theory]
    [InlineData(null, NextWorkoutState.None)]
    [InlineData(SessionStatus.Planned, NextWorkoutState.NotStarted)]
    [InlineData(SessionStatus.Skipped, NextWorkoutState.Started)]
    [InlineData(SessionStatus.InProgress, NextWorkoutState.Started)]
    [InlineData(SessionStatus.Completed, NextWorkoutState.Started)]
    public void TheNextWorkout_IsReadFromItsStatus(SessionStatus? status, NextWorkoutState expected)
    {
        Assert.Equal(expected, SessionCorrection.NextWorkoutOf(status));
    }
}
