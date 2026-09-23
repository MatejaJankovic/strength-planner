namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Calculates estimated one-rep max and derived working weights using the Epley formula.
/// </summary>
public sealed class E1RmCalculator
{
    /// <summary>
    /// Estimates one-rep max with Epley formula over effective reps (reps + RIR):
    /// 1RM = weight * (1 + (reps + rir) / 30). Epley assumes a set to failure, so reps
    /// left in reserve count as additional effective reps. Supported only up to the
    /// configured rep cap (actual reps), above which the estimate is unreliable.
    /// </summary>
    public decimal EstimateOneRepMax(decimal weight, int reps, int rir = 0)
    {
        if (reps > TrainingConstants.EpleyRepCap)
        {
            throw new ArgumentException(
                $"Epley estimate is supported only for {TrainingConstants.EpleyRepCap} reps or fewer.",
                nameof(reps));
        }

        if (rir < 0)
        {
            throw new ArgumentException("RIR cannot be negative.", nameof(rir));
        }

        var effectiveReps = reps + rir;
        return weight * (1 + effectiveReps / 30m);
    }

    /// <summary>
    /// Whether a logged set may produce an e1RM estimate at all.
    ///
    /// Three conditions, and each one exists because the estimate would otherwise be
    /// fiction: there must be a load to scale, the rep count must stay inside the Epley
    /// range, and the set must have ended near failure. A set of 12 reps at RIR 5 reads
    /// 156.7 kg where the same set to failure reads 140 — a 12% invention that then lived
    /// in the 56-day window as the best estimate.
    ///
    /// Exposed as one predicate so that the session summary, the fatigue score and any
    /// later caller share a single definition.
    /// </summary>
    public static bool CanEstimateFrom(decimal loadKg, int reps, int rir)
    {
        return loadKg > 0
               && reps > 0
               && reps <= TrainingConstants.EpleyRepCap
               && rir >= 0
               && rir <= TrainingConstants.E1RmMaxRir;
    }

    /// <summary>
    /// Best estimate over the sets that may produce one, or null when none qualifies.
    /// A null result means no record, no personal best and no e1RM chip — the same path a
    /// session logged above the rep cap already took.
    /// </summary>
    public decimal? BestEstimate(IEnumerable<LoggedSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        decimal? best = null;

        foreach (var set in sets)
        {
            if (!CanEstimateFrom(set.WeightKg, set.Reps, set.Rir))
            {
                continue;
            }

            var estimate = EstimateOneRepMax(set.WeightKg, set.Reps, set.Rir);
            if (best is null || estimate > best.Value)
            {
                best = estimate;
            }
        }

        return best;
    }

    /// <summary>
    /// Calculates working weight by reversing Epley with effective reps = target reps + target RIR,
    /// then rounds to the exercise's load increment (2.5 kg when none is supplied).
    /// </summary>
    public decimal WorkingWeightFor(decimal oneRepMax, int targetReps, int targetRir, decimal? weightStepKg = null)
    {
        var effectiveReps = targetReps + targetRir;
        var rawWeight = oneRepMax / (1 + effectiveReps / 30m);

        return WeightMath.RoundToStep(rawWeight, weightStepKg ?? TrainingConstants.WeightStepKg);
    }
}
