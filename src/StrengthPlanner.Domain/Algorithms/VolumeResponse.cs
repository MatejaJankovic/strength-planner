namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// What one completed training week says about how a muscle group handled its volume.
///
/// Two counts, deliberately. A set far from failure produces fatigue without producing
/// stimulus, so the two questions the system asks need different measures: "was this
/// productive?" is answered by the stimulative count, "was this tiring?" by the raw one.
/// Using the stimulative count for both would let a week of junk volume look like rest.
/// </summary>
/// <param name="PerformedSets">
/// Weekly working sets that actually counted: each set's contribution (1.0 to the primary
/// muscle, 0.5 to each secondary) scaled by how close it came to failure.
/// </param>
/// <param name="RawSets">
/// The same contributions without the failure-proximity scaling — every set performed,
/// however easy. This is the fatigue measure.
/// </param>
/// <param name="AverageRirDeviation">
/// Mean of (effective RIR - target RIR) over the sets that counted, weighted the same way
/// they are. Positive means the week was easier than prescribed, negative that it was
/// harder.
/// </param>
/// <param name="FailureShare">
/// Share of the week's sets taken to failure, measured against <paramref name="RawSets"/>
/// so that it keeps meaning "how much of the week ended in failure" rather than "how much
/// of the productive work ended in failure".
/// </param>
public sealed record VolumeResponse(
    decimal PerformedSets,
    decimal RawSets,
    decimal AverageRirDeviation,
    decimal FailureShare);
