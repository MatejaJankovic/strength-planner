namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Computes next-session targets using double progression with RIR-based daily auto-regulation.
/// </summary>
public sealed class ProgressionEngine
{
    /// <summary>
    /// Applies RIR correction = clamp((average effective RIR - target RIR) / (30 + middle of
    /// the range + target RIR), +/-10%) and double progression rules. The rate per RIR point
    /// is Epley's (<see cref="CorrectionPerRirPoint"/>): 2.7% for 3-6 at RIR 2, 2.4% for 8-12
    /// at RIR 1 and 2.2% for an isolation at 10-20.
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
    /// the whole story - 8 kg to 10 kg is 25%, where an 8-12 range absorbs about 13%. So in a
    /// wide range (one at least as wide as its target reserve) the rule above applies only
    /// where the step fits the prescription (<see cref="StepAbsorption.FitsAtTarget"/>): for
    /// 8-12 at RIR 1 that is a dumbbell from about 15 kg, a bar or cable from about 19 kg and
    /// a machine from about 38 kg. Below that, the step is given only when every set's own
    /// capacity - its reps plus the reserve it really left - absorbs it
    /// (<see cref="StepAbsorption.Absorbs"/>), and it goes to the next load the rack has
    /// (<see cref="WeightMath.StepAbove"/>), or as far as the correction alone asks if that is
    /// more - never a step and a correction on top of it, which rounding turns into a second
    /// step. Until then the load waits: it is never lowered, and a positive correction applies
    /// exactly as it would inside the range.
    ///
    /// A narrow or fixed prescription (5 x 5, 11-12 at RIR 2) keeps the rule above at every
    /// load. It absorbs little by construction - a fixed target absorbs nothing at the target
    /// reserve - so judging it by capacity moved ordinary barbell work into the light-load
    /// regime: a squat of 80 kg done 5 x 5 at RIR 1, exactly as prescribed, stopped stepping,
    /// and a fixed 12 on an 8 kg dumbbell could not step even at RIR 5. Found in review. The
    /// reps are the prescription there and the reserve carries the step, as round 10 decided;
    /// on a light load the coarse step then remains a limitation of the prescription.
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
            deviation * CorrectionPerRirPoint(repRangeMin, repRangeMax, targetRir),
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        var allHitTop = workingSets.All(set => set.Reps >= repRangeMax);

        decimal nextWeight;
        var atBodyweightFloor = false;

        if (!allHitTop)
        {
            nextWeight = ApplyCorrection(
                usedTotalKg,
                correction,
                stepKg,
                bodyweightLoadKg,
                usedWeightKg,
                farHarderThanPlanned: deviation <= -TrainingConstants.StepDownRirShortfall);
            atBodyweightFloor = BodyweightLoad.IsAtBodyweightFloor(usedTotalKg * (1 + correction), bodyweightLoadKg);
        }
        else if (StepAbsorption.IsNarrow(repRangeMin, repRangeMax, targetRir)
                 || StepAbsorption.FitsAtTarget(usedTotalKg, stepKg, repRangeMin, repRangeMax, targetRir))
        {
            // Korak staje u propis, ili je propis uzak (5 x 5, 11-12 sa RIR 2) pa korak nosi
            // rezerva: pravilo iz runda 9 i 10, bez izmene.
            nextWeight = RangeResetCoversShortfall(deviation, repRangeMin, repRangeMax)
                ? StepUp(usedTotalKg, correction, stepKg, bodyweightLoadKg)
                // Uska nedelja (11-12 sa RIR 2, 3-4 sa RIR 3) izvučena preko cilja: po
                // Epley-u sledeći propis ne ide na većoj težini. Vrh opsega ipak nikad ne
                // spušta opterećenje, pa se zadržava tačno ono što je podignuto.
                : usedWeightKg;
        }
        else if (EverySetAbsorbs(
                     workingSets,
                     usedTotalKg,
                     WeightMath.StepAbove(usedWeightKg, stepKg) - usedWeightKg,
                     repRangeMin))
        {
            // Korak je velik za ovu težinu (laka bučica, mala mašina), ali ga je svaka serija
            // svojim kapacitetom - ponavljanja plus rezerva koja je zaista ostala - upila.
            // Jedan korak, ili koliko sama korekcija traži ako je to više - ali ne korak PLUS
            // korekcija: kod teške šipke korekcija povrh koraka dodaje deo koraka, a ovde bi
            // je zaokruživanje pretvorilo u ceo drugi (12 kg uz 3 x 12 @RIR4 je davalo 16 kg,
            // gde po Epley-u ostaje 4.5 ponavljanja). Ni manje od same korekcije: ista serija
            // jedno ponavljanje ispod vrha je dobija, pa bi vrh inače davao manje.
            //
            // I to sledeća težina koju stalak ima, a ne zaokruženo "podignuto + korak": 15 kg
            // na koraku od 2 kg je 16, a zaokruživanje 17 daje 18 - skok koji kapacitet nije
            // ni proveravao.
            nextWeight = Math.Max(
                WeightMath.StepAbove(usedWeightKg, stepKg),
                ApplyCorrection(usedTotalKg, Math.Max(0m, correction), stepKg, bodyweightLoadKg, usedWeightKg));
        }
        else
        {
            // Vrh opsega je dostignut, ali korak još ne staje: težina čeka. Ne pada - vrh
            // opsega nikad ne spušta opterećenje - a korekcija naviše prolazi isto kao unutar
            // opsega. Da je ovde strožija, serija na vrhu bi dobijala manje od iste serije
            // jedno ponavljanje ispod njega (izmereno: 28 -> 30 kg ispod vrha, 28 na vrhu).
            nextWeight = ApplyCorrection(usedTotalKg, Math.Max(0m, correction), stepKg, bodyweightLoadKg, usedWeightKg);
        }

        return new ProgressionResult(
            nextWeight,
            WeightIncreased: nextWeight > usedWeightKg,
            LoadFloorReached: atBodyweightFloor);
    }

    /// <summary>
    /// How much load one point of RIR deviation is worth, read from the same Epley curve as
    /// the estimate, the working weight and the step absorption.
    ///
    /// A lifter who did r reps with e in reserve at load w has an e1RM of
    /// w * (30 + r + e) / 30. Doing r reps at the target reserve t takes
    /// w * (30 + r + e) / (30 + r + t), so the correction is (e - t) / (30 + r + t): linear
    /// in the deviation, at a rate set by how many reps the set carries.
    ///
    /// r is the middle of the prescribed range, not the reps of the session. With the
    /// session's own reps, 10 reps at RIR 3 got a smaller correction than 9 reps at RIR 3
    /// (4.9% against 5.0%): correct by Epley, since the stronger lifter will also do more
    /// reps next time, but it breaks the rule that more reps never propose a lighter load,
    /// for a difference rounding hides anyway. A session that does not reach the top sits
    /// anywhere from the floor to one rep below it, and the middle halves the worst error of
    /// either end.
    ///
    /// It used to be a flat 3% per point, the top of the handbook's "one rep is about 2-3%
    /// of load". By Epley that holds for a triple and is too much for everything the plan
    /// prescribes above it: at the middle of the range it is 2.7% for 3-6 at RIR 2, 2.4% for
    /// 8-12 at RIR 1 and 2.2% for an isolation at 10-20, so an easy lateral raise was
    /// corrected about 40% more than its reps justify, and the next session overshot.
    /// </summary>
    public static decimal CorrectionPerRirPoint(int repRangeMin, int repRangeMax, int targetRir)
    {
        var middleReps = (repRangeMin + repRangeMax) / 2m;

        return 1m / (TrainingConstants.EpleyRepDivisor + middleReps + targetRir);
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
    /// Whether every set's capacity - its reps plus the reserve it really left - keeps the
    /// floor of the range reachable after <paramref name="increaseKg"/> more load.
    /// </summary>
    private static bool EverySetAbsorbs(
        IReadOnlyList<WorkingSet> workingSets,
        decimal usedTotalKg,
        decimal increaseKg,
        int repRangeMin)
    {
        return workingSets.All(set => StepAbsorption.Absorbs(
            usedTotalKg,
            increaseKg,
            repRangeMin,
            set.Reps,
            Math.Max(0, set.EffectiveRir(repRangeMin))));
    }

    /// <summary>
    /// One step up from the used load, with only a positive correction stacked on it.
    ///
    /// The reset to the floor of the range already pays for a RIR shortfall, and a negative
    /// correction would pay for it a second time. So it was: 0.97u + 2.5 cancelled the step
    /// between ~42 and 125 kg and lowered the load above 125 kg (160 -> 140 kg over eight
    /// sessions, under an upward arrow).
    /// </summary>
    private static decimal StepUp(decimal usedTotalKg, decimal correction, decimal stepKg, decimal bodyweightLoadKg)
    {
        return BodyweightLoad.AddedTarget(
            (usedTotalKg * (1 + Math.Max(0m, correction))) + stepKg,
            bodyweightLoadKg,
            stepKg);
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
    /// The step can also be too coarse to express the correction at all. Downward, whenever
    /// 10% of the load is no more than half a step - up to and including five steps:
    /// dumbbells up to 10 kg, cables and bars up to 12.5 kg, machines up to 25 kg - even the
    /// largest correction rounds back onto the used load. (Upward the same holds only below
    /// five steps: rounding away from zero lifts exactly half a step up, so +10% on 10 kg
    /// becomes 12.) A lateral raise at 10 kg done for 5, 4 and 4 reps of an 8-12 range asked
    /// for 9 kg and got 10, session after session - the lifter stayed below the range until
    /// the reps crept back up on their own. A session at least
    /// <see cref="TrainingConstants.StepDownRirShortfall"/> reps harder than its target
    /// reserve is the strongest signal the rule acts on, so when rounding erases its
    /// correction the load moves down by one step instead - unless that would leave an
    /// externally loaded lift empty (a 2 kg dumbbell has no lighter one), where the load
    /// stays. Upward the same erasure is left alone: holding the load there only means the
    /// reps keep climbing toward the step.
    ///
    /// The trigger used to be "the correction reached the -10% cap", which at 3% a point
    /// meant a shortfall of 3.33 reps. With the Epley rate the cap sits at 3.7-4.7 reps, and
    /// the very case above (5, 4 and 4 at RIR 1, a shortfall of 3.67) stopped reaching it and
    /// went back to holding 10 kg. The decision is about the session, so it is now stated in
    /// reps rather than read off the arithmetic of the load.
    /// </summary>
    private static decimal ApplyCorrection(
        decimal usedTotalKg,
        decimal correction,
        decimal stepKg,
        decimal bodyweightLoadKg,
        decimal usedWeightKg,
        bool farHarderThanPlanned = false)
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
        var erasedFarHarderThanPlanned = lowered >= usedWeightKg && farHarderThanPlanned;

        if (!erasedFarHarderThanPlanned)
        {
            return lowered;
        }

        // Korak ispod težine od jednog koraka je prazna ruka (bučica od 2 kg -> 0), koju
        // ekran s pravom prijavljuje kao grešku - tu težina ostaje. Kod vežbe sa telesnom
        // masom nula dodatih je stvarno opterećenje, pa tamo korak ispod postoji.
        var stepBelowKg = WeightMath.StepBelow(usedWeightKg, stepKg);

        return stepBelowKg > 0 || bodyweightLoadKg > 0 ? stepBelowKg : usedWeightKg;
    }
}
