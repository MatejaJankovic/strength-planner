namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// What one completed training week says about how a muscle group handled its volume.
/// </summary>
/// <param name="PerformedSets">
/// Weekly working sets for the muscle group, fractional because a set contributes 1.0
/// to the primary muscle and 0.5 to each secondary one.
/// </param>
/// <param name="AverageRirDeviation">
/// Mean of (effective RIR - target RIR) over every set touching the muscle group.
/// Positive means the week was easier than prescribed, negative that it was harder.
/// </param>
/// <param name="FailureShare">Share of those sets that were taken to failure, 0 to 1.</param>
public sealed record VolumeResponse(
    decimal PerformedSets,
    decimal AverageRirDeviation,
    decimal FailureShare);
