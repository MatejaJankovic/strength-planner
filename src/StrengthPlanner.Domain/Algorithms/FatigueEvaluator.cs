namespace StrengthPlanner.Domain.Algorithms;

/// <summary>One logged set with the prescription it was performed against.</summary>
/// <param name="Set">The set as the progression engine sees it.</param>
/// <param name="RepRangeMin">Floor of the prescribed rep range.</param>
/// <param name="TargetRir">Prescribed reps in reserve.</param>
/// <param name="Weight">
/// How much this set counts in the average. One for the fatigue score, which asks about
/// the week as a whole; the volume limits weigh each set by what it contributed to the
/// muscle group and by how close to failure it came, since that is the measure they learn
/// from. The parameter exists so both sides can share <b>one</b> definition of the signal:
/// they had two, and the two disagreed by two whole RIR points on the same three sets.
/// </param>
public sealed record RirSample(WorkingSet Set, int RepRangeMin, int TargetRir, decimal Weight = 1m);

/// <summary>
/// Scores accumulated fatigue from one completed training week and decides whether the
/// next week should become a deload.
///
/// The planned deload is tied to the calendar — week four of four — which is a guess
/// about when fatigue will have accumulated, not a measurement of it. A lifter who
/// stalls in week two carries that fatigue for a fortnight; one still progressing in
/// week four is deloaded for no reason. The calendar deload stays as a floor, but a
/// week that shows enough fatigue can now pull the next one forward.
///
/// Four signals are combined, each normalised to 0..1 and weighted. No single signal
/// can trigger a deload on its own: the heaviest carries 0.35 against a threshold of
/// 0.60, so at least two have to agree. That is deliberate — every one of them is
/// noisy in isolation, and an unnecessary deload costs a week of training.
///
/// For that safeguard to mean anything the signals have to be independent, which is why
/// the RIR signal is measured only over sets the lifter completed. A set taken to
/// failure short of the range would otherwise push both the RIR average and the failure
/// share, and the "two must agree" rule would be satisfied by a single event.
///
/// That held for every week except the extreme one, where it was undone by a special case:
/// a week with no completed sets at all used to read the RIR signal as 1.0 - its worst
/// value - while the failure share was already 1.0 for the same reason. 0.35 + 0.25 is
/// exactly the threshold, so "every set went to failure" triggered a deload by itself. It
/// now contributes nothing, because a week with nothing completed holds no RIR evidence,
/// and the failure share is the signal that fact belongs to.
/// </summary>
public static class FatigueEvaluator
{
    /// <summary>Score at or above which the next week is turned into a deload.</summary>
    public const decimal DeloadThreshold = 0.60m;

    // Odstupanje RIR-a se meri u odnosu na ono što je za dati cilj uopšte dostižno
    // (WeeklyFatigue.AchievableRirDeficit), a ne u odnosu na fiksne dve jedinice.
    // Fiksna skala bi hipertrofiju (ciljni RIR 1) trajno stavila u podređen položaj:
    // najgore što serija bez otkaza tamo može da prijavi je -1, pa nikada ne bi mogla
    // da iskoristi ni polovinu ovog udela.
    private const decimal RirWeight = 0.35m;

    // Polovina serija do otkaza je nedelja izvučena preko svake mere.
    private const decimal FailureShareAtFullWeight = 0.5m;
    private const decimal FailureWeight = 0.25m;

    // Pad procenjenog 1RM od 5% je jasan gubitak performansi, ne dnevna oscilacija.
    private const decimal E1RmDropAtFullWeight = 0.05m;
    private const decimal E1RmWeight = 0.25m;

    // Ispod 80% MRV-a volumen ne doprinosi; na samom MRV-u doprinosi u punoj meri.
    private const decimal VolumeShareFloor = 0.80m;
    private const decimal VolumeShareAtFullWeight = 1.00m;
    private const decimal VolumeWeight = 0.15m;

    /// <summary>
    /// Returns the fatigue score of the week, from 0 (fresh) to 1 (every signal maxed).
    /// </summary>
    public static decimal Score(WeeklyFatigue fatigue)
    {
        ArgumentNullException.ThrowIfNull(fatigue);

        // Nedelja bez ijedne dovršene serije nema prosek koji bi se merio, pa ovaj signal
        // doprinosi nulom. Ranije je takvo odsustvo čitano kao najgore moguće očitavanje
        // (1.0), što je istu činjenicu — "sve je išlo do otkaza" — pustilo da puni i ovaj
        // signal i udeo otkaza: 0.35 + 0.25 = tačno prag, iz jednog uzroka. Merenjem se
        // videlo i kao litica: dvadeset otkaza je davalo 0.60, a devetnaest otkaza uz JEDNU
        // dovršenu seriju na cilju 0.25.
        var rir = Normalize(-fatigue.AverageRirDeviation, 0m, Math.Max(1m, fatigue.AchievableRirDeficit));
        var failures = Normalize(fatigue.FailureShare, 0m, FailureShareAtFullWeight);
        var e1Rm = Normalize(-fatigue.E1RmChangeShare, 0m, E1RmDropAtFullWeight);
        var volume = Normalize(fatigue.VolumeVsMrvShare, VolumeShareFloor, VolumeShareAtFullWeight);

        return rir * RirWeight
               + failures * FailureWeight
               + e1Rm * E1RmWeight
               + volume * VolumeWeight;
    }

    /// <summary>
    /// Weighted mean of (effective RIR - target RIR) over the sets the lifter completed,
    /// i.e. those not taken to failure; 0 when there are none. Failures are left out so
    /// that this signal and the failure share stay two separate measurements.
    ///
    /// Effective RIR is <see cref="WorkingSet.EffectiveRir"/>, the measure progression uses.
    /// A set stopped below the range floor with reserve left is therefore "harder than
    /// planned" here as well; read as raw RIR it scored as easier, so the same set pulled
    /// progression down and the fatigue score toward "fresh".
    ///
    /// This is the <b>only</b> definition of the signal. The volume limits used to compute
    /// their own over every set including failures, which on the same three sets read -2
    /// where this reads 0 - and there a single failed set blocked both halves of the "had
    /// reps to spare" test, which is an AND. Same name, two measures, one of them counting
    /// one event twice.
    /// </summary>
    public static decimal AverageRirDeviation(IEnumerable<RirSample> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var completed = sets.Where(sample => !sample.Set.IsFailure).ToList();
        var weight = completed.Sum(sample => sample.Weight);

        if (weight == 0m)
        {
            return 0m;
        }

        return completed.Sum(sample =>
            sample.Weight * (sample.Set.EffectiveRir(sample.RepRangeMin) - sample.TargetRir)) / weight;
    }

    /// <summary>
    /// True when the week's fatigue justifies deloading the next one.
    /// </summary>
    public static bool ShouldDeload(WeeklyFatigue fatigue)
    {
        return Score(fatigue) >= DeloadThreshold;
    }

    /// <summary>
    /// Maps a raw signal onto 0..1, where <paramref name="floor"/> contributes nothing
    /// and <paramref name="ceiling"/> contributes fully.
    /// </summary>
    private static decimal Normalize(decimal value, decimal floor, decimal ceiling)
    {
        if (ceiling <= floor)
        {
            return 0m;
        }

        return Math.Clamp((value - floor) / (ceiling - floor), 0m, 1m);
    }
}
