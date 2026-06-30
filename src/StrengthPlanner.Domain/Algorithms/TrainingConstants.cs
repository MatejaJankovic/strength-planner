namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Shared training algorithm constants used by progression, auto-regulation and e1RM calculations.
/// </summary>
public static class TrainingConstants
{
    public const decimal RpeCorrectionPerPoint = 0.03m;
    public const decimal MaxCorrection = 0.10m;
    public const decimal WeightStepKg = 2.5m;
    public const decimal DeloadWeightFactor = 0.90m;
    public const int EpleyRepCap = 10;
}
