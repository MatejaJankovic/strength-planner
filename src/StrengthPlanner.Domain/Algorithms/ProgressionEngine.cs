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
    /// positive correction is stacked on it. By Epley that is the condition under which the
    /// next prescription can be lifted at or above the <b>used</b> load:
    /// (30 + max + rir) / (30 + min + target) &gt;= 1, i.e. deviation + (max - min) &gt;= 0.
    /// Only a narrow week with a large target RIR (e.g. 11-12 at RIR 2, taken to failure)
    /// fails that condition; the load then holds exactly. Reaching the top of the range
    /// never lowers the load, at any weight or step.
    ///
    /// The condition speaks about the used load, and the proposal is the used load plus a
    /// step. For a barbell that difference is a rounding error; for a light dumbbell it is
    /// the whole story - 8 kg to 10 kg is 25%, where an 8-12 range absorbs about 10%. So
    /// "the top" is <see cref="StepAbsorption.RepsToEarnStep"/>: the top of the range when
    /// the step fits it, and otherwise the rep count at which it does. Between the two the
    /// session is judged like any other within the range, and the load waits.
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
        decimal? weightStepKg = null,
        decimal bodyweightLoadKg = 0m)
    {
        ArgumentNullException.ThrowIfNull(workingSets);

        if (workingSets.Count == 0)
        {
            return new ProgressionResult(usedWeightKg, WeightIncreased: false);
        }

        var stepKg = weightStepKg ?? TrainingConstants.WeightStepKg;

        // Sve računice idu nad UKUPNIM opterećenjem — telo koje se diže je opterećenje kao i
        // tegovi — a rezultat se vraća u ono što vežbač stavlja na pojas. Bez tela, kod
        // vežbi sa telesnom masom, korekcija od 3% se primenjivala na dodatne kilograme, pa
        // je zgib sa +10 kg "rastao" za 0.3 kg umesto za 2.7.
        var usedTotalKg = usedWeightKg + bodyweightLoadKg;

        // Nema šta da se skalira: ni tela, ni tegova (npr. plank upisan sa 0 kg). Napredak
        // ide kroz ponavljanja, a ne kroz "+1 kg" na vežbi koja se ne opterećuje.
        if (usedTotalKg <= 0)
        {
            return new ProgressionResult(0m, WeightIncreased: false, LoadFloorReached: true);
        }

        var averageRir = workingSets.Average(set => (decimal)set.EffectiveRir(repRangeMin));
        var deviation = averageRir - targetRir;
        var correction = Math.Clamp(
            deviation * TrainingConstants.RpeCorrectionPerPoint,
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        // Vrh koji donosi korak: gornja granica opsega, osim kad je korak prevelik da bi ga
        // povratak na dno opsega upio (laka bučica, mala mašina) - tada tek broj ponavljanja
        // na kome ga upija. Ekran treninga računa isti broj i prikazuje ga kao cilj.
        var repsToEarnStep = StepAbsorption.RepsToEarnStep(
            usedTotalKg,
            stepKg,
            repRangeMin,
            repRangeMax,
            targetRir);
        var allReachedRangeTop = workingSets.All(set => set.Reps >= repRangeMax);
        var allHitTop = workingSets.All(set => set.Reps >= repsToEarnStep);

        decimal nextWeight;
        var atBodyweightFloor = false;

        if (!allReachedRangeTop)
        {
            nextWeight = ApplyCorrection(usedTotalKg, correction, stepKg, bodyweightLoadKg, usedWeightKg);
            atBodyweightFloor = BodyweightLoad.IsAtBodyweightFloor(usedTotalKg * (1 + correction), bodyweightLoadKg);
        }
        else if (!allHitTop)
        {
            // Vrh opsega je dostignut, ali korak još ne staje u opseg. Težina čeka dok
            // ponavljanja ne stignu do cilja koji ga upija. Vrh opsega ni ovde ne spušta
            // opterećenje - korekcija naniže bi 40 kg na vrhu opsega pretvorila u 35 -
            // a korekcija naviše sme da prođe, jer je izvedena iz stvarne rezerve.
            nextWeight = ApplyCorrection(usedTotalKg, Math.Max(0m, correction), stepKg, bodyweightLoadKg, usedWeightKg);
        }
        else if (RangeResetCoversShortfall(deviation, repRangeMin, repsToEarnStep))
        {
            // Vrh opsega: sledeći trening kreće od dna, a to vredi (max - min) ponavljanja
            // rezerve. Manjak RIR-a do te granice je već plaćen tim povratkom; negativna
            // korekcija bi ga platila drugi put. Tako je i bilo: 0.97u + 2.5 je poništavalo
            // korak između ~42 i 125 kg, a iznad 125 kg obaralo opterećenje (160 -> 140 kg
            // za osam treninga, uz strelicu naviše).
            nextWeight = BodyweightLoad.AddedTarget(
                (usedTotalKg * (1 + Math.Max(0m, correction))) + stepKg,
                bodyweightLoadKg,
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
            WeightIncreased: nextWeight > usedWeightKg,
            LoadFloorReached: atBodyweightFloor);
    }

    /// <summary>
    /// Whether resetting the target from the top that earned the step to the floor of the
    /// range covers the RIR shortfall of that session. By Epley the next prescription then
    /// loads at least the used weight. <paramref name="topReps"/> is the top of the range
    /// unless the step is too coarse for it — see <see cref="StepAbsorption"/>.
    /// </summary>
    private static bool RangeResetCoversShortfall(decimal deviation, int repRangeMin, int topReps)
    {
        return deviation + (topReps - repRangeMin) >= 0;
    }

    /// <summary>
    /// Scales the used load by the correction and rounds to the exercise's step, without
    /// letting the rounding reverse the direction of the correction.
    ///
    /// Rounding to the nearest step can cross the used weight when that weight does not sit
    /// on the step grid — which it need not, since it comes from what the lifter logged. A
    /// harder-than-planned session at 107 kg with a -1% correction gives 105.93 kg, which
    /// rounds up to 110 kg on a 10 kg step, so the load rises after a session that asked for
    /// less. (This comment first used 102 kg on a 2.5 kg step, where 100.98 rounds to 100
    /// and nothing reverses — the regression test always used the 107 kg case.)
    /// The sign of the correction is the decision; the step is only how fine the result can
    /// be expressed. With no correction at all the used weight is kept as it is.
    ///
    /// The step can also be too coarse to express the correction at all: whenever 10% of
    /// the load is no more than half a step (dumbbells up to 10 kg, cables and bars up to
    /// 12.5 kg, machines up to 25 kg), even the largest correction rounds back onto the
    /// used load. A lateral raise at 10 kg done for 5, 4 and 4 reps of an 8-12 range asked
    /// for 9 kg and got 10, session after session - the lifter stayed below the range until
    /// the reps crept back up on their own. A correction that reached the cap is the
    /// strongest signal the rule knows, so when rounding erases it the load moves down by
    /// one step instead. Upward the same erasure is left alone: holding the load there only
    /// means the reps keep climbing toward the step.
    /// </summary>
    private static decimal ApplyCorrection(
        decimal usedTotalKg,
        decimal correction,
        decimal stepKg,
        decimal bodyweightLoadKg,
        decimal usedWeightKg)
    {
        if (correction == 0)
        {
            return usedWeightKg;
        }

        var rounded = BodyweightLoad.AddedTarget(usedTotalKg * (1 + correction), bodyweightLoadKg, stepKg);

        if (correction > 0)
        {
            return Math.Max(rounded, usedWeightKg);
        }

        var lowered = Math.Min(rounded, usedWeightKg);
        var erasedAtTheCap = lowered >= usedWeightKg && correction <= -TrainingConstants.MaxCorrection;

        return erasedAtTheCap
            ? WeightMath.StepBelow(usedWeightKg, stepKg)
            : lowered;
    }
}
