using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Whether a finished workout can still be corrected, and what a correction does.
///
/// Finishing a workout spends its sets: they become an estimated maximum, the target of the
/// same day next week, the fatigue and volume of the week. A correction is allowed while that
/// is still undoable, which is exactly while nothing has been built on top of it. Once the
/// next workout of the same day is under way, or the block that followed has been generated
/// from the estimates, the sets have been used and changing them would rewrite history the
/// lifter has already acted on.
/// </summary>
public static class SessionCorrection
{
    /// <summary>
    /// The state of the session this one hands its target to: the same day, nearest later
    /// week. Only a planned one can still take a new target without anything having been
    /// decided on it.
    ///
    /// A skipped one counts as started. Skipping is an answer, and it settles the week: the
    /// week can close and be scored from these sets, and when it was the last workout of the
    /// block the next block is generated from their estimates, so by then they have been used.
    /// Putting it back on the plan reopens the window.
    /// </summary>
    public static NextWorkoutState NextWorkoutOf(SessionStatus? nextStatus)
    {
        return nextStatus switch
        {
            null => NextWorkoutState.None,
            SessionStatus.Planned => NextWorkoutState.NotStarted,
            _ => NextWorkoutState.Started
        };
    }

    /// <summary>
    /// <paramref name="consequencesRecorded"/> is false for workouts completed before the
    /// consequences were recorded per workout: nothing ties their estimate or their week's
    /// landmark moves to them, so they can be corrected as history only and nothing is
    /// recalculated.
    /// </summary>
    public static CorrectionWindow Evaluate(
        SessionStatus status,
        bool consequencesRecorded,
        NextWorkoutState next,
        bool nextBlockOpened)
    {
        var mode = consequencesRecorded ? CorrectionMode.Recalculate : CorrectionMode.HistoryOnly;

        if (status != SessionStatus.Completed)
        {
            return new CorrectionWindow(false, mode, CorrectionBlock.NotCompleted);
        }

        if (next == NextWorkoutState.Started)
        {
            return new CorrectionWindow(false, mode, CorrectionBlock.NextWorkoutStarted);
        }

        // With no later week to hand a target to, what the sets fed is the next block, which
        // starts from the estimates. Once it is generated they have been used.
        if (next == NextWorkoutState.None && nextBlockOpened)
        {
            return new CorrectionWindow(false, mode, CorrectionBlock.NextBlockOpened);
        }

        return new CorrectionWindow(true, mode, CorrectionBlock.None);
    }
}

/// <summary>The same-day session of the nearest later week, as far as a correction cares.</summary>
public enum NextWorkoutState
{
    /// <summary>No later week has this day: the workout is in the last week of its block.</summary>
    None,

    /// <summary>Planned: it can still take a new target.</summary>
    NotStarted,

    /// <summary>Under way, finished or skipped: it was prescribed from the sets being corrected, or the week was settled without it.</summary>
    Started
}

/// <summary>What saving a correction does.</summary>
public enum CorrectionMode
{
    /// <summary>The estimate, the next target and the week's score are worked out again.</summary>
    Recalculate,

    /// <summary>Only the sets change, along with what is read from them (history, analytics).</summary>
    HistoryOnly
}

/// <summary>Why a finished workout can no longer be corrected.</summary>
public enum CorrectionBlock
{
    None,
    NotCompleted,
    NextWorkoutStarted,
    NextBlockOpened
}

/// <summary>Whether a correction is possible now, how it is applied, and what blocks it.</summary>
public sealed record CorrectionWindow(bool IsOpen, CorrectionMode Mode, CorrectionBlock BlockedBy);
