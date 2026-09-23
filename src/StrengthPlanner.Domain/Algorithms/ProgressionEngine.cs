namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Computes next-session targets using double progression with RIR-based daily auto-regulation.
/// </summary>
public sealed class ProgressionEngine
{
    /// <summary>
    /// Applies RIR correction = clamp((average effective RIR - target RIR) * 3%, +/-10%)
    /// and double progression rules.
    ///
    /// Effective RIR comes from <see cref="WorkingSet.EffectiveRir"/>: below the range floor
    /// it is the lifter's capacity measured against the floor, which is what lets the
    /// correction reach the same 10% cap downward as it does upward.
    ///
    /// When not every set reached the top of the range, the next load is the used load
    /// scaled by the correction.
    ///
    /// When every set reached the top, the next session starts again from the bottom of
    /// the range, and that reset is worth (max - min) reps of margin. A RIR shortfall up to
    /// that size is therefore already paid for, so the step is added in full and only a
    /// positive correction is stacked on it. By Epley this is exactly the condition under
    /// which the next prescription can be lifted at or above the used load:
    /// (30 + max + rir) / (30 + min + target) &gt;= 1, i.e. deviation + (max - min) &gt;= 0.
    /// Only a narrow week with a large target RIR (e.g. 11-12 at RIR 2, taken to failure)
    /// fails that condition; the load then holds exactly. Reaching the top of the range
    /// never lowers the load, at any weight or step.
    ///
    /// The load increment used for the double-progression step and for rounding comes
    /// from the exercise (2.5 kg when none is supplied), so dumbbells and machines step
    /// realistically. Rounding never reverses the direction of the correction, and with no
    /// correction the used load is kept as it is — see <see cref="ApplyCorrection"/>.
    /// <see cref="ProgressionResult.WeightIncreased"/> reports whether the result is
    /// heavier than the load used, not merely that the top was reached.
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

        var averageRir = workingSets.Average(set => (decimal)set.EffectiveRir(repRangeMin));
        var deviation = averageRir - targetRir;
        var correction = Math.Clamp(
            deviation * TrainingConstants.RpeCorrectionPerPoint,
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        var allHitTop = workingSets.All(set => set.Reps >= repRangeMax);

        decimal nextWeight;

        if (!allHitTop)
        {
            nextWeight = ApplyCorrection(usedWeightKg, correction, stepKg);
        }
        else if (RangeResetCoversShortfall(deviation, repRangeMin, repRangeMax))
        {
            // Vrh opsega: sledeći trening kreće od dna, a to vredi (max - min) ponavljanja
            // rezerve. Manjak RIR-a do te granice je već plaćen tim povratkom; negativna
            // korekcija bi ga platila drugi put. Tako je i bilo: 0.97u + 2.5 je poništavalo
            // korak između ~42 i 125 kg, a iznad 125 kg obaralo opterećenje (160 -> 140 kg
            // za osam treninga, uz strelicu naviše).
            nextWeight = WeightMath.RoundToStep(
                (usedWeightKg * (1 + Math.Max(0m, correction))) + stepKg,
                stepKg);
        }
        else
        {
            // Uska nedelja (11-12 sa RIR 2, 3-4 sa RIR 3) izvučena preko cilja: po Epley-u
            // sledeći propis ne ide na većoj težini. Vrh opsega ipak nikad ne spušta
            // opterećenje, pa se zadržava tačno ono što je podignuto.
            nextWeight = usedWeightKg;
        }

        return new ProgressionResult(
            nextWeight,
            repRangeMin,
            WeightIncreased: nextWeight > usedWeightKg);
    }

    /// <summary>
    /// Whether resetting the target from the top of the range to its floor covers the RIR
    /// shortfall of a top-of-range session. By Epley the next prescription then loads at
    /// least the used weight.
    /// </summary>
    private static bool RangeResetCoversShortfall(decimal deviation, int repRangeMin, int repRangeMax)
    {
        return deviation + (repRangeMax - repRangeMin) >= 0;
    }

    /// <summary>
    /// Scales the used load by the correction and rounds to the exercise's step, without
    /// letting the rounding reverse the direction of the correction.
    ///
    /// Rounding to the nearest step can cross the used weight when that weight does not sit
    /// on the step grid — which it need not, since it comes from what the lifter logged. A
    /// harder-than-planned session at 102 kg with a -1% correction rounds 100.98 kg up to
    /// 102.5 kg on a 2.5 kg step, so the load rises after a session that asked for less.
    /// The sign of the correction is the decision; the step is only how fine the result can
    /// be expressed. With no correction at all the used weight is kept as it is.
    /// </summary>
    private static decimal ApplyCorrection(decimal usedWeightKg, decimal correction, decimal stepKg)
    {
        if (correction == 0)
        {
            return usedWeightKg;
        }

        var rounded = WeightMath.RoundToStep(usedWeightKg * (1 + correction), stepKg);

        return correction < 0
            ? Math.Min(rounded, usedWeightKg)
            : Math.Max(rounded, usedWeightKg);
    }
}
