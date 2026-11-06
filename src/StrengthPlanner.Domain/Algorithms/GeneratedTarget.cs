using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Which stored load targets still carry the number the generator derived from a maximum.
/// </summary>
public static class GeneratedTarget
{
    /// <summary>
    /// The one week whose load the generator stores, from the maximum known at generation.
    ///
    /// Later weeks get their stored load when the same day of the previous week is completed
    /// (from the sets lifted, or in a periodized block from the maximum known at that moment),
    /// or have none and are computed on read from the freshest maximum.
    /// </summary>
    public const int StoredWeek = 1;

    /// <summary>
    /// True for a target a new maximum should rewrite: the stored week, in a session the
    /// lifter has not touched.
    ///
    /// Without this, week one was the one place where a new maximum or test set changed
    /// nothing: a lifter back from a pause who logged a lighter test set still saw the old
    /// load (round 16, #99). "Not touched" is the definition <c>WeeklySetPlanner</c> uses: no
    /// set logged, and not started. A skipped session counts as untouched, so that unskipping
    /// it later brings back the current load and not the one from before the new record. A
    /// started session belongs to the lifter, and its load is not moved under them.
    /// </summary>
    public static bool FollowsTheMaximum(int weekNumber, SessionStatus status, bool hasLoggedSets)
    {
        return weekNumber == StoredWeek
               && !hasLoggedSets
               && status is SessionStatus.Planned or SessionStatus.Skipped;
    }
}
