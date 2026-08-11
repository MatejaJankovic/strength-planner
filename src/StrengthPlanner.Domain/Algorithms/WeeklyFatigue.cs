namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// The four fatigue signals a completed training week produces.
/// </summary>
/// <param name="AverageRirDeviation">
/// Mean of (effective RIR - target RIR) over every working set of the week. Negative
/// means the lifter had less in reserve than the plan asked for.
/// </param>
/// <param name="FailureShare">Share of the week's sets taken to failure, 0 to 1.</param>
/// <param name="E1RmChangeShare">
/// Relative change in the week's best estimated 1RM against the previous week, as a
/// share (-0.03 is a 3% drop). Zero when there is no previous week to compare against,
/// which correctly contributes nothing rather than guessing.
/// </param>
/// <param name="VolumeVsMrvShare">
/// Highest ratio of performed weekly sets to MRV across all muscle groups. 1.0 means at
/// least one muscle group sat exactly on its ceiling.
/// </param>
public sealed record WeeklyFatigue(
    decimal AverageRirDeviation,
    decimal FailureShare,
    decimal E1RmChangeShare,
    decimal VolumeVsMrvShare);
