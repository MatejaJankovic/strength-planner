namespace StrengthPlanner.Domain.Algorithms;

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

        // Nedelja u kojoj nijedna serija nije dovršena nema prosek koji bi se merio,
        // ali to odsustvo je najgore moguće očitavanje, a ne neutralno.
        var rir = fatigue.AllSetsFailed
            ? 1m
            : Normalize(-fatigue.AverageRirDeviation, 0m, Math.Max(1m, fatigue.AchievableRirDeficit));
        var failures = Normalize(fatigue.FailureShare, 0m, FailureShareAtFullWeight);
        var e1Rm = Normalize(-fatigue.E1RmChangeShare, 0m, E1RmDropAtFullWeight);
        var volume = Normalize(fatigue.VolumeVsMrvShare, VolumeShareFloor, VolumeShareAtFullWeight);

        return rir * RirWeight
               + failures * FailureWeight
               + e1Rm * E1RmWeight
               + volume * VolumeWeight;
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
