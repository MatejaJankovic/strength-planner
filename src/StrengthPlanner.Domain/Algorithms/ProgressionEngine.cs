namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Computes next-session targets using double progression with RIR-based daily auto-regulation.
/// </summary>
public sealed class ProgressionEngine
{
    /// <summary>
    /// Applies RIR correction = clamp((average RIR - target RIR) * 3%, +/-10%) and double progression rules.
    /// </summary>
    public ProgressionResult ComputeNext(
        decimal usedWeightKg,
        IReadOnlyList<(int reps, int rir)> workingSets,
        int targetRir,
        int repRangeMin,
        int repRangeMax)
    {
        ArgumentNullException.ThrowIfNull(workingSets);

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
            ? adjustedWeight + TrainingConstants.WeightStepKg
            : adjustedWeight;
        var nextTargetReps = repRangeMin;

        return new ProgressionResult(
            WeightMath.RoundToStep(nextWeight, TrainingConstants.WeightStepKg),
            nextTargetReps,
            allHitTop);
    }
}
