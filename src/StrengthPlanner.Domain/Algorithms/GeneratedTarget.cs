using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Which stored load targets still carry the number the generator derived from a maximum.
/// </summary>
public static class GeneratedTarget
{
    /// <summary>
    /// True for a target a new maximum should rewrite: week one, in a session not yet started.
    ///
    /// The generator stores a load only in week one, from the maximum known at generation.
    /// Every later week is written by progression from the sets actually lifted, or is
    /// computed on read from the freshest maximum. So week one was the one place where a new
    /// maximum or test set changed nothing: a lifter back from a pause who logged a lighter
    /// test set still saw the old load (round 16, #99). A session already started belongs to
    /// the lifter, and its load is not moved under them.
    /// </summary>
    public static bool FollowsTheMaximum(int weekNumber, SessionStatus status)
    {
        return weekNumber == 1 && status == SessionStatus.Planned;
    }
}
