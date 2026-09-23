namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Result of the next-session progression calculation.
/// </summary>
/// <param name="NextWeightKg">Load proposed for the next session of the same exercise.</param>
/// <param name="NextTargetReps">Rep target the next session starts from (the range floor).</param>
/// <param name="WeightIncreased">
/// True only when <paramref name="NextWeightKg"/> is heavier than the load that was used.
/// It used to mirror "every set reached the top of the range", which the summary showed as
/// an arrow even when the proposal was equal to or lower than the lifted weight.
/// </param>
/// <param name="LoadFloorReached">
/// True when the rule wanted a load lighter than the lifter's own body and the proposal is
/// therefore body mass alone. Progression then continues through reps, not kilograms.
/// </param>
public sealed record ProgressionResult(
    decimal NextWeightKg,
    int NextTargetReps,
    bool WeightIncreased,
    bool LoadFloorReached = false);
