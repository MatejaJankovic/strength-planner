using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Which set allocation a block actually gets.
/// </summary>
public static class SetAllocationRule
{
    /// <summary>
    /// The requested allocation for a custom template, and target-volume balancing for a
    /// built-in one.
    ///
    /// Following the template exists for the lifter's own template, where a typed "3 sets"
    /// is an intention. A built-in template carries an exercise layout, not an intention
    /// about volume, and its prescription is not built to respect the recovery ceiling:
    /// followed verbatim, periodized blocks put muscle-weeks above MRV at every level (67
    /// advanced, 284 intermediate, 223 beginner across the built-in weeks; first noted in
    /// docs/features/experience-volume-consistency.md), while the screen only said the week
    /// "may stay below the target". Balancing is what keeps a built-in week between MEV and
    /// MRV, so it is not optional there.
    /// </summary>
    /// <param name="requested">What the request asked for.</param>
    /// <param name="isCustomTemplate">Whether the block's template is the lifter's own.</param>
    public static SetAllocation For(SetAllocation requested, bool isCustomTemplate)
    {
        return isCustomTemplate ? requested : SetAllocation.TargetVolume;
    }
}
