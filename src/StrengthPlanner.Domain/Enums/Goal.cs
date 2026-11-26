namespace StrengthPlanner.Domain.Enums;

public enum Goal
{
    Strength,
    Hypertrophy,

    /// <summary>
    /// Keep what a previous block built, with the least training that does it: the weekly volume sits at
    /// the lifter's MEV, the loads are held and never pushed, and the limits are not learned from it.
    /// </summary>
    Maintenance
}
