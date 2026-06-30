namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Calculates estimated one-rep max and derived working weights using the Epley formula.
/// </summary>
public sealed class E1RmCalculator
{
    /// <summary>
    /// Estimates one-rep max with Epley formula: 1RM = weight * (1 + reps / 30), only up to the configured rep cap.
    /// </summary>
    public decimal EstimateOneRepMax(decimal weight, int reps)
    {
        if (reps > TrainingConstants.EpleyRepCap)
        {
            throw new ArgumentException(
                $"Epley estimate is supported only for {TrainingConstants.EpleyRepCap} reps or fewer.",
                nameof(reps));
        }

        return weight * (1 + reps / 30m);
    }

    /// <summary>
    /// Calculates working weight by reversing Epley with effective reps = target reps + target RIR, then rounds to 2.5 kg.
    /// </summary>
    public decimal WorkingWeightFor(decimal oneRepMax, int targetReps, int targetRir)
    {
        var effectiveReps = targetReps + targetRir;
        var rawWeight = oneRepMax / (1 + effectiveReps / 30m);

        return WeightMath.RoundToStep(rawWeight, TrainingConstants.WeightStepKg);
    }
}
