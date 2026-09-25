namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// What one completed training week says about how a muscle group handled its volume.
///
/// Three measures, and they answer three different questions. "Was this productive?" is
/// answered by the strength change; "was this tiring?" by the raw count and the failure
/// share; "how much of it counted?" by the stimulative count. Using one of them for two
/// questions is what this record exists to prevent.
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
/// Weighted mean of (effective RIR - target RIR) over the sets the lifter <b>completed</b>,
/// from <see cref="FatigueEvaluator.AverageRirDeviation"/> — one definition shared with the
/// fatigue score. Negative means the completed work was harder than the plan asked for.
///
/// This is a statement about <b>load</b>, which is why only the recovery ceiling reads it.
/// A week that felt easy means the weights were light, and progression corrects weights;
/// reading it as "this lifter needs more volume" was one correction too many for one cause.
/// </param>
/// <param name="FailureShare">
/// Share of the week's sets taken to failure, measured against <paramref name="RawSets"/>
/// so that it keeps meaning "how much of the week ended in failure" rather than "how much
/// of the productive work ended in failure".
/// </param>
/// <param name="StrengthChangeShare">
/// Relative change in what this muscle group's exercises could lift, against the last
/// comparable week, as a share (-0.03 is a 3% decline). From
/// <see cref="StrengthChange.ChangeShare"/>, so only like-for-like sets are compared.
///
/// This is the evidence about <b>stimulus</b>, and it is what the minimum and the target
/// now learn from: adaptation is the only thing that can say whether a volume was worth
/// doing. Null when the week has nothing comparable to measure against — the first week of
/// a block, or a week whose rep counts do not line up with the previous one. A missing
/// measurement moves nothing.
/// </param>
public sealed record VolumeResponse(
    decimal PerformedSets,
    decimal RawSets,
    decimal AverageRirDeviation,
    decimal FailureShare,
    decimal? StrengthChangeShare = null);
