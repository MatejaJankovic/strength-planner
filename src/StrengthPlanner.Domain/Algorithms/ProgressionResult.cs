namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Result of the next-session progression calculation.
/// </summary>
public sealed record ProgressionResult(decimal NextWeightKg, int NextTargetReps, bool WeightIncreased);
