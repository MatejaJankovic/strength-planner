namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Moves a user's MEV/MRV limits toward the volume they actually tolerate.
///
/// The seeded landmarks are population averages: every lifter starts on the same
/// numbers per muscle group even though real tolerance varies widely. Rather than ask
/// the lifter to guess their own limits, each completed (non-deload) training week is
/// read as evidence — how much volume was performed, how much was left in reserve, and
/// how often sets ended in failure — and the limits move by at most one set per week.
///
/// One set per week is deliberately slow. The signal is noisy (sleep, stress, food),
/// and a landmark that chases a single bad week would be no more trustworthy than the
/// static value it replaced.
/// </summary>
public static class VolumeAdaptation
{
    /// <summary>Most a landmark can move in a single week, in weekly working sets.</summary>
    public const int MaxWeeklyStep = 1;

    /// <summary>How far a personal landmark may drift from its seeded value, as a share.</summary>
    public const decimal MaxDriftFromSeed = 0.50m;

    /// <summary>Minimum gap kept between MEV and MRV so the optimal band never collapses.</summary>
    public const int MinBandWidth = 2;

    // Volumen ispod ovog udela MRV-a ne govori ništa o gornjoj granici: lakoća na
    // pola posla nije dokaz da bi i pun posao bio podnošljiv.
    private const decimal NearMrvShare = 0.90m;

    // Odstupanje RIR-a mora da pređe ceo poen da bi se uzelo kao signal; sve ispod
    // toga je šum procene, pogotovo kod početnika.
    private const decimal MeaningfulRirDeviation = 1m;

    // Otkazi na četvrtini serija su jasan znak da je nedelja bila pretemna.
    private const decimal FatigueFailureShare = 0.25m;

    /// <summary>
    /// Returns the landmarks to store after a completed week. <paramref name="seed"/> is
    /// the population default the personal value is allowed to drift around.
    /// </summary>
    public static VolumeLandmarkValues Adjust(
        VolumeLandmarkValues current,
        VolumeLandmarkValues seed,
        VolumeResponse response)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(response);

        var showedFatigue = response.AverageRirDeviation <= -MeaningfulRirDeviation
                            || response.FailureShare >= FatigueFailureShare;
        var hadRepsToSpare = response.AverageRirDeviation >= 0
                             && response.FailureShare == 0;

        var mrv = current.Mrv;
        var mev = current.Mev;

        // Gornja granica: pomera se samo ako je nedelja stvarno bila blizu nje.
        if (response.PerformedSets >= current.Mrv * NearMrvShare && hadRepsToSpare)
        {
            mrv += MaxWeeklyStep;
        }
        else if (response.PerformedSets >= current.Mev && showedFatigue)
        {
            mrv -= MaxWeeklyStep;
        }

        // Donja granica: sudi se samo kada je nedelja i bila na donjoj granici,
        // jer o minimalnoj dozi ništa ne govori nedelja odrađena znatno iznad nje.
        if (response.PerformedSets <= current.Mev)
        {
            if (response.AverageRirDeviation >= MeaningfulRirDeviation)
            {
                mev += MaxWeeklyStep;
            }
            else if (showedFatigue)
            {
                mev -= MaxWeeklyStep;
            }
        }

        mev = ClampToSeed(mev, seed.Mev);
        mrv = ClampToSeed(mrv, seed.Mrv);

        // MEV ne sme da pojede optimalni pojas ni kada obe granice udare u svoje ivice.
        if (mrv - mev < MinBandWidth)
        {
            mev = mrv - MinBandWidth;
        }

        return new VolumeLandmarkValues(Math.Max(1, mev), mrv);
    }

    private static int ClampToSeed(int value, int seed)
    {
        var lower = (int)Math.Ceiling(seed * (1 - MaxDriftFromSeed));
        var upper = (int)Math.Floor(seed * (1 + MaxDriftFromSeed));

        return Math.Clamp(value, Math.Max(1, lower), Math.Max(1, upper));
    }
}
