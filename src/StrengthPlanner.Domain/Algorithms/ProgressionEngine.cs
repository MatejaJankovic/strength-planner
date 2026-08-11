namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Computes next-session targets using double progression with RIR-based daily auto-regulation.
/// </summary>
public sealed class ProgressionEngine
{
    /// <summary>
    /// Applies RIR correction = clamp((average effective RIR - target RIR) * 3%, +/-10%)
    /// and double progression rules. Failed sets contribute a negative effective RIR
    /// proportional to the reps missed against the bottom of the range, which is what
    /// lets the correction reach the same 10% cap downward as it does upward.
    /// The load increment used for the double-progression bump and for rounding comes
    /// from the exercise (2.5 kg when none is supplied), so dumbbells and machines
    /// step realistically.
    /// </summary>
    public ProgressionResult ComputeNext(
        decimal usedWeightKg,
        IReadOnlyList<WorkingSet> workingSets,
        int targetRir,
        int repRangeMin,
        int repRangeMax,
        decimal? weightStepKg = null)
    {
        ArgumentNullException.ThrowIfNull(workingSets);

        if (workingSets.Count == 0)
        {
            return new ProgressionResult(usedWeightKg, repRangeMin, WeightIncreased: false);
        }

        var stepKg = weightStepKg ?? TrainingConstants.WeightStepKg;

        decimal rirTotal = 0;
        var allHitTop = true;
        var anyFailure = false;

        for (var i = 0; i < workingSets.Count; i++)
        {
            var set = workingSets[i];
            rirTotal += set.EffectiveRir(repRangeMin);

            if (set.Reps < repRangeMax)
            {
                allHitTop = false;
            }

            if (set.IsFailure)
            {
                anyFailure = true;
            }
        }

        var averageRir = rirTotal / workingSets.Count;
        var deviation = averageRir - targetRir;
        var correction = Math.Clamp(
            deviation * TrainingConstants.RpeCorrectionPerPoint,
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        var adjustedWeight = usedWeightKg * (1 + correction);

        // Double progression dodaje korak tek kada je ceo rep-opseg ispunjen bez otkaza:
        // dvanaest ponavljanja izvučenih do otkaza nije isti signal kao dvanaest sa RIR 1.
        var increaseWeight = allHitTop && !anyFailure;
        var nextWeight = increaseWeight
            ? adjustedWeight + stepKg
            : adjustedWeight;
        var nextTargetReps = repRangeMin;

        return new ProgressionResult(
            WeightMath.RoundToStep(nextWeight, stepKg),
            nextTargetReps,
            increaseWeight);
    }
}
