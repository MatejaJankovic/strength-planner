namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// The fatigue signals a completed training week produces.
/// </summary>
/// <param name="AverageRirDeviation">
/// Mean of (effective RIR - target RIR) over the sets the lifter actually <b>completed</b>,
/// i.e. excluding sets taken to failure. Effective RIR is <see cref="WorkingSet.EffectiveRir"/>,
/// the same measure progression uses, so a set that stopped below the range floor with
/// reserve reads as harder here too. Negative means the completed work was harder than
/// the plan asked for. Failed sets are deliberately left out so that this signal and
/// <paramref name="FailureShare"/> measure two different things: one failed set would
/// otherwise drive both, and two signals that always move together are not two signals.
/// </param>
/// <param name="AchievableRirDeficit">
/// How far below target a completed set inside the range can land — the plan's target
/// RIR, never below 1. A hypertrophy block targeting RIR 1 can only ever report -1 there,
/// while a strength block targeting RIR 2 can report -2; without this the same grinding
/// week would score differently purely because of the goal. A set stopped below the range
/// floor can read further below target; the score clamps it to the full weight.
/// </param>
/// <param name="FailureShare">
/// Share of the week's sets taken to failure, 0 to 1. This is where "the week went to
/// failure" is measured, and it is the only place: a companion flag used to push the RIR
/// signal to its worst value for the same reason, which let one fact reach the deload
/// threshold on its own.
/// </param>
/// <param name="E1RmChangeShare">
/// Relative change in the week's best estimated 1RM against the most recent comparable
/// week, as a share (-0.03 is a 3% drop). Zero when there is nothing to compare
/// against, which correctly contributes nothing rather than reading a missing value as
/// a decline.
/// </param>
/// <param name="VolumeVsMrvShare">
/// Highest ratio of performed weekly sets to MRV across all muscle groups. 1.0 means at
/// least one muscle group sat exactly on its ceiling.
/// </param>
public sealed record WeeklyFatigue(
    decimal AverageRirDeviation,
    decimal AchievableRirDeficit,
    decimal FailureShare,
    decimal E1RmChangeShare,
    decimal VolumeVsMrvShare);
