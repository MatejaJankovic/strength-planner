namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Moves a user's MEV/MAV/MRV limits toward the volume they actually tolerate.
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

    /// <summary>
    /// Minimum gap kept between MEV and MRV so the optimal band never collapses. Two is
    /// the smallest gap that still leaves room for MAV strictly between them.
    /// </summary>
    public const int MinBandWidth = 2;

    // Volumen ispod ovog udela MRV-a ne govori ništa o gornjoj granici: lakoća na
    // pola posla nije dokaz da bi i pun posao bio podnošljiv.
    private const decimal NearMrvShare = 0.90m;

    // Isti razlog važi i za ciljni volumen: nedelja odrađena znatno ispod MAV-a ne
    // govori ništa o tome da li je MAV dobro postavljen.
    private const decimal NearMavShare = 0.90m;

    // Koliko cilj sme da odluta naviše u odnosu na mesto koje mu seed daje u pojasu.
    // Dovoljno da se uči, premalo da se slepi za plafon.
    private const decimal TargetDriftShare = 0.15m;

    // Odstupanje RIR-a mora da pređe ceo poen da bi se uzelo kao signal; sve ispod
    // toga je šum procene, pogotovo kod početnika.
    private const decimal MeaningfulRirDeviation = 1m;

    // Otkazi na četvrtini serija su jasan znak da je nedelja bila pretemna.
    private const decimal FatigueFailureShare = 0.25m;

    // Poslednja serija izvučena do otkaza je uobičajena praksa i ne sme da znači da
    // gornja granica više nikada ne može da poraste; tek iznad ovog udela otkaz počinje
    // da govori o umoru.
    private const decimal TolerableFailureShare = FatigueFailureShare / 2;

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

        // Isti prag u oba smera: ispod celog RIR poena razlika je šum procene, pa ne sme
        // da pomeri granicu ni naviše ni naniže.
        var showedFatigue = response.AverageRirDeviation <= -MeaningfulRirDeviation
                            || response.FailureShare >= FatigueFailureShare;
        var hadRepsToSpare = response.AverageRirDeviation >= MeaningfulRirDeviation
                             && response.FailureShare <= TolerableFailureShare;

        var mrv = current.Mrv;
        var mav = current.Mav;
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

        // Ciljni volumen: pomera se kada je nedelja odrađena na njemu ili iznad njega.
        // MAV je jedina granica oko koje se zaista trenira, pa je i jedina o kojoj svaka
        // takva nedelja nešto kaže — za razliku od MEV-a i MRV-a, koji se dodiruju retko.
        if (response.PerformedSets >= current.Mav * NearMavShare)
        {
            if (hadRepsToSpare)
            {
                mav += MaxWeeklyStep;
            }
            else if (showedFatigue)
            {
                mav -= MaxWeeklyStep;
            }
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
        mav = ClampToSeed(mav, seed.Mav);
        mrv = ClampToSeed(mrv, seed.Mrv);

        // Optimalni pojas se čuva podizanjem MRV-a kad god je to moguće. Spuštanje MEV-a
        // je jednosmerno: donja granica se posle diže samo ako je nedelja odrađena NA njoj,
        // pa bi MEV oboren zbog uskog pojasa — bez ijednog dokaza o samoj donjoj granici —
        // ostao zaglavljen i pošto se pojas ponovo otvori.
        if (mrv - mev < MinBandWidth)
        {
            var liftedMrv = ClampToSeed(mev + MinBandWidth, seed.Mrv);

            if (liftedMrv - mev >= MinBandWidth)
            {
                mrv = liftedMrv;
            }
            else
            {
                mev = mrv - MinBandWidth;
            }
        }

        // Pojas mora da preživi i poravnanje MEV-a na jedinicu, inače bi ovo prošlo
        // algoritam a palo na CHECK ograničenju usred završavanja treninga.
        var safeMev = Math.Max(1, mev);
        var safeMrv = Math.Max(safeMev + MinBandWidth, mrv);

        return new VolumeLandmarkValues(safeMev, ClampTarget(mav, safeMev, safeMrv, seed), safeMrv);
    }

    /// <summary>
    /// Drži cilj unutar pojasa i na razumnom odstojanju od plafona.
    ///
    /// Prag za pomeranje MAV-a je slabiji od praga za MRV (jer je MAV niži), pa MAV raste
    /// na svakoj dobroj nedelji na kojoj raste i MRV — i na nizu onih na kojima MRV ne
    /// raste. Bez ograničenja bi se vremenom slepio za plafon i ekran bi kao cilj nudio
    /// volumen jednu seriju ispod onoga koji je sistem proglasio nepodnošljivim, što je
    /// tačno suprotno od definicije MAV-a ("bez ulaska u prekomeran zamor").
    ///
    /// Granica je zato mesto koje seed propisuje u pojasu, uvećano za dozvoljeno lutanje.
    /// Bez tog dodatka MAV ne bi bio naučen nego samo izveden iz MEV-a i MRV-a, pa ne bi
    /// ni imao smisla kao zasebna vrednost.
    /// </summary>
    private static int ClampTarget(int value, int mev, int mrv, VolumeLandmarkValues seed)
    {
        var seedBand = seed.Mrv - seed.Mev;
        var seedShare = seedBand > 0
            ? (seed.Mav - seed.Mev) / (decimal)seedBand
            : 0.5m;

        var allowedShare = Math.Min(1m, seedShare + TargetDriftShare);
        var ceiling = mev + (int)Math.Round((mrv - mev) * allowedShare, MidpointRounding.AwayFromZero);

        return Math.Clamp(value, mev + 1, Math.Clamp(ceiling, mev + 1, mrv - 1));
    }

    private static int ClampToSeed(int value, int seed)
    {
        var lower = (int)Math.Ceiling(seed * (1 - MaxDriftFromSeed));
        var upper = (int)Math.Floor(seed * (1 + MaxDriftFromSeed));

        return Math.Clamp(value, Math.Max(1, lower), Math.Max(1, upper));
    }
}
