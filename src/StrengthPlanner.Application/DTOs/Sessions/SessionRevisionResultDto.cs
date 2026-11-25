namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>
/// What saving a correction of a finished workout changed. <see cref="Result"/> has the shape
/// of what finishing the workout returns, so the same summary can show it.
/// </summary>
public class SessionRevisionResultDto
{
    public CompleteSessionResultDto Result { get; set; } = new();

    /// <summary>The week's fatigue score before the correction. Null while the week is open.</summary>
    public decimal? PreviousFatigueScore { get; set; }

    /// <summary>The week's fatigue score after it. Null while the week is open.</summary>
    public decimal? FatigueScore { get; set; }

    /// <summary>
    /// A deload that finishing the workout pulled forward stays, although the corrected sets
    /// would not have called for one. Rewriting the next week back would change a plan the
    /// lifter may have looked at.
    /// </summary>
    public bool DeloadKept { get; set; }
}

/// <summary>Whether a finished workout can be corrected, and how a correction is applied.</summary>
public class SessionCorrectionDto
{
    public bool CanCorrect { get; set; }

    /// <summary>"Recalculate", or "HistoryOnly" for a workout finished before consequences were recorded.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Why it cannot be corrected: "NextWorkoutStarted" or "NextBlockOpened". Null when it can.</summary>
    public string? BlockedBy { get; set; }

    /// <summary>Sets were changed and the correction has not been saved yet.</summary>
    public bool RevisionPending { get; set; }
}
