namespace StrengthPlanner.Domain.Algorithms;

/// <summary>What a week asks of one exercise: rep range and reps in reserve.</summary>
/// <param name="RepRangeMin">Floor of the rep range.</param>
/// <param name="RepRangeMax">Top of the rep range.</param>
/// <param name="TargetRir">Reps the lifter should leave in reserve.</param>
public sealed record LoadPrescription(int RepRangeMin, int RepRangeMax, int TargetRir)
{
    /// <summary>Whether another week asks for exactly the same thing.</summary>
    public bool Matches(LoadPrescription other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return RepRangeMin == other.RepRangeMin
               && RepRangeMax == other.RepRangeMax
               && TargetRir == other.TargetRir;
    }
}

/// <summary>
/// The load the same exercise gets next week.
///
/// The rules used to live inside <c>SessionService</c>, and one branch of that method
/// skipped them: an exercise with no logged sets copied its planned weight forward
/// untouched, so a deload week inherited 100% of it instead of 90%, and a periodized week
/// that prescribes a different rep range inherited a weight derived for the old one.
/// Deciding it in one place means an exercise that was skipped is treated like one that was
/// trained — just without evidence of its own.
/// </summary>
public static class NextWeekLoad
{
    /// <summary>
    /// Load for the next week, or null when nothing is known about the exercise at all.
    /// A null result leaves the stored target untouched rather than erasing it.
    /// </summary>
    /// <param name="referenceWeightKg">
    /// Heaviest load lifted this session (<see cref="WorkingLoad.ReferenceWeightKg"/>), or the
    /// planned weight when nothing was logged.
    /// </param>
    /// <param name="progressionWeightKg">
    /// What <see cref="ProgressionEngine"/> proposed, or null when there was nothing to judge.
    /// </param>
    /// <param name="current">Prescription this session was performed against.</param>
    /// <param name="next">Prescription of the week being filled in.</param>
    /// <param name="nextIsDeload">Whether that week is a deload.</param>
    /// <param name="oneRepMaxKg">Recent estimated one-rep max, or null. Always a total load.</param>
    /// <param name="weightStepKg">Smallest load increment of the exercise.</param>
    /// <param name="bodyweightLoadKg">
    /// Body mass this exercise will carry next week, or zero for external load. Every input
    /// and the result are <b>added</b> kilograms; the rules themselves work on the total.
    /// </param>
    public static decimal? For(
        decimal? referenceWeightKg,
        decimal? progressionWeightKg,
        LoadPrescription current,
        LoadPrescription next,
        bool nextIsDeload,
        decimal? oneRepMaxKg,
        decimal weightStepKg,
        decimal bodyweightLoadKg = 0m)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var calculator = new E1RmCalculator();

        // Rasterećenje: 90% onoga što je STVARNO podignuto, bez progresije. Kad nije
        // podignuto ništa, ide 90% težine izvedene iz maksimuma za tekući propis - deload ne
        // sme da nasledi punu težinu samo zato što je vežba preskočena. (Serije se polove
        // odvojeno, u periodizaciji; ovde je reč samo o opterećenju.)
        if (nextIsDeload)
        {
            var baseTotalKg = referenceWeightKg is not null
                ? referenceWeightKg.Value + bodyweightLoadKg
                : oneRepMaxKg is null
                    ? (decimal?)null
                    : calculator.WorkingLoadFor(oneRepMaxKg.Value, current.RepRangeMin, current.TargetRir);

            return baseTotalKg is null
                ? null
                : BodyweightLoad.AddedTarget(
                    baseTotalKg.Value * TrainingConstants.DeloadWeightFactor,
                    bodyweightLoadKg,
                    weightStepKg);
        }

        if (next.Matches(current))
        {
            return progressionWeightKg
                   ?? referenceWeightKg
                   ?? WorkingLoadOrNull(calculator, oneRepMaxKg, next, weightStepKg, bodyweightLoadKg);
        }

        // Naredna nedelja traži drugačiji propis, pa nošenje iste težine nema smisla:
        // nedelja koja pada sa 12 na 5 ponavljanja mora da bude teža. Težina se izvodi iz
        // procene maksimuma i propisa te nedelje, isto kao pri generisanju prve nedelje.
        if (oneRepMaxKg is not null)
        {
            return WorkingLoadOrNull(calculator, oneRepMaxKg, next, weightStepKg, bodyweightLoadKg);
        }

        // Bez procene maksimuma se ona izvodi iz same težine: ono što je planirano (ili
        // odrađeno) za tekući propis nosi svoj implicitni maksimum, pa se iz njega dobija
        // težina za novi propis. Nošenje neizmenjene težine u drugi rep-opseg je jedino što
        // je sigurno pogrešno.
        var known = progressionWeightKg ?? referenceWeightKg;
        if (known is null)
        {
            return null;
        }

        var impliedOneRepMax = calculator.EstimateOneRepMax(
            known.Value + bodyweightLoadKg,
            current.RepRangeMin,
            current.TargetRir);

        return BodyweightLoad.AddedTarget(
            calculator.WorkingLoadFor(impliedOneRepMax, next.RepRangeMin, next.TargetRir),
            bodyweightLoadKg,
            weightStepKg);
    }

    /// <summary>
    /// Recovers the load a deload week was derived from: it carries 90% of what was lifted
    /// before it, so dividing that factor out restores the reference.
    ///
    /// A deload is a pause, not a step back. Progressing from its lightened sets would write
    /// a load below the one already earned into the week that follows — which exists whenever
    /// fatigue pulled the deload forward and released the planned one.
    ///
    /// Rounded <b>down</b> to the step on purpose. The deload weight was itself rounded, so
    /// the division is not an exact inverse: on a 10 kg step a deload of 50 kg (derived from
    /// 50) divides to 55.6, and rounding to the nearest step would restore 60 kg — a load
    /// the lifter never touched. Rounding down can only ever restore the same load or one
    /// step less.
    ///
    /// The factor applies to the total, so the body portion is added before dividing and
    /// taken off again after: a pull-up deloaded to body mass alone must not restore
    /// "0 / 0.9 = 0 added" when the body it carries was itself lightened by a belt.
    /// </summary>
    public static decimal? UndoDeload(
        decimal? deloadWeightKg,
        decimal weightStepKg,
        decimal bodyweightLoadKg = 0m)
    {
        if (deloadWeightKg is null)
        {
            return null;
        }

        var restoredTotalKg =
            (deloadWeightKg.Value + bodyweightLoadKg) / TrainingConstants.DeloadWeightFactor;

        return Math.Max(0m, WeightMath.FloorToStep(restoredTotalKg - bodyweightLoadKg, weightStepKg));
    }

    /// <summary>
    /// How much the next load differs from what was lifted, or null when either is unknown.
    /// The summary reports this instead of "every set reached the top of the range".
    /// </summary>
    public static decimal? ChangeKg(decimal? referenceWeightKg, decimal? nextWeightKg)
    {
        if (referenceWeightKg is null || nextWeightKg is null)
        {
            return null;
        }

        return nextWeightKg.Value - referenceWeightKg.Value;
    }

    private static decimal? WorkingLoadOrNull(
        E1RmCalculator calculator,
        decimal? oneRepMaxKg,
        LoadPrescription prescription,
        decimal weightStepKg,
        decimal bodyweightLoadKg)
    {
        return oneRepMaxKg is null
            ? null
            : BodyweightLoad.AddedTarget(
                calculator.WorkingLoadFor(
                    oneRepMaxKg.Value,
                    prescription.RepRangeMin,
                    prescription.TargetRir),
                bodyweightLoadKg,
                weightStepKg);
    }
}
