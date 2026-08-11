namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Computes next-session targets using double progression with RIR-based daily auto-regulation.
/// </summary>
public sealed class ProgressionEngine
{
    /// <summary>
    /// Applies RIR correction = clamp((average RIR - target RIR) * 3%, +/-10%) and double progression rules.
    /// The load increment used for the double-progression bump and for rounding comes from the
    /// exercise (2.5 kg when none is supplied), so dumbbells and machines step realistically.
    /// </summary>
    public ProgressionResult ComputeNext(
        decimal usedWeightKg,
        IReadOnlyList<(int reps, int rir)> workingSets,
        int targetRir,
        int repRangeMin,
        int repRangeMax,
        decimal? weightStepKg = null)
    {
        ArgumentNullException.ThrowIfNull(workingSets);

        var stepKg = weightStepKg ?? TrainingConstants.WeightStepKg;

        if (workingSets.Count == 0)
        {
            return new ProgressionResult(usedWeightKg, repRangeMin, WeightIncreased: false);
        }

        decimal rirTotal = 0;
        var allHitTop = true;

        for (var i = 0; i < workingSets.Count; i++)
        {
            var set = workingSets[i];
            rirTotal += set.rir;

            if (set.reps < repRangeMax)
            {
                allHitTop = false;
            }
        }

        var averageRir = rirTotal / workingSets.Count;
        var deviation = averageRir - targetRir;
        var correction = Math.Clamp(
            deviation * TrainingConstants.RpeCorrectionPerPoint,
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        var adjustedWeight = usedWeightKg * (1 + correction);

        var nextWeight = allHitTop
            ? adjustedWeight + stepKg
            : adjustedWeight;
        var nextTargetReps = repRangeMin;

        return new ProgressionResult(
            WeightMath.RoundToStep(nextWeight, stepKg),
            nextTargetReps,
            allHitTop);
    }
}
