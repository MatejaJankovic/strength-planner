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
    /// lets the correction reach the same 10% cap downward as it does upward. A set
    /// that failed at the top of the range is penalised by that correction alone; the
    /// double-progression increment still applies, because reaching the top of the
    /// range is the very signal the increment is based on.
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

        for (var i = 0; i < workingSets.Count; i++)
        {
            var set = workingSets[i];
            rirTotal += set.EffectiveRir(repRangeMin);

            if (set.Reps < repRangeMax)
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

        // Dostizanje vrha opsega je signal na kome double progression počiva, pa ga
        // otkaz ne poništava: serija koja je otkazala ISPOD vrha ionako već obara
        // allHitTop. Razliku između "12 do otkaza" i "12 sa RIR 1" nosi korekcija
        // (efektivni RIR 0 daje -3%), a ne drugo, skriveno kažnjavanje — spajanje
        // to dvoje je opterećenje na vrhu opsega guralo naniže iz treninga u trening.
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
