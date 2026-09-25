namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Moves a user's MEV/MAV/MRV limits toward the volume they actually tolerate.
///
/// The seeded landmarks are population averages: every lifter starts on the same
/// numbers per muscle group even though real tolerance varies widely. Rather than ask
/// the lifter to guess their own limits, each completed (non-deload) training week is
/// read as evidence, and the limits move by at most one set per week.
///
/// One set per week is deliberately slow. The signal is noisy (sleep, stress, food),
/// and a landmark that chases a single bad week would be no more trustworthy than the
/// static value it replaced.
///
/// <b>Which evidence answers which question</b> is the part that had to be corrected.
/// The limits used to be driven by the RIR deviation — how the sets felt against their
/// target — for all three. But RIR is a statement about the <i>load</i>, and the load is
/// what progression corrects: a week that felt easy means the weights were light, and the
/// next session already gets heavier ones. Reading the same fact a second time as "this
/// lifter needs more volume" made one cause move two dials, and with the set balancing in
/// between it closed a loop: measured, a week at MAV whose sets were two RIR easier than
/// planned raised MAV from 16 to 17 <i>and</i> pushed the load from 100 kg to 107.5.
///
/// So the three limits now read the evidence that belongs to them:
///
/// <list type="bullet">
/// <item><b>MRV is about recovery</b>, and RIR shortfall, failures and a real strength
/// decline are all recovery signals. It keeps them.</item>
/// <item><b>MAV and MEV are about stimulus</b>, and the only evidence of stimulus is
/// adaptation — <see cref="VolumeResponse.StrengthChangeShare"/>. A week with nothing
/// comparable to measure against moves neither of them, which is the honest answer rather
/// than a guess.</item>
/// </list>
///
/// The old rule for MEV was also backwards, which is what the audit reported: "a week at
/// MEV that felt easy means MEV must be higher". A week at MEV that <i>produced progress</i>
/// means MEV is at most that much — the minimum effective dose is smaller than it was
/// thought to be, not larger.
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

    /// <summary>
    /// How much the estimated strength of a muscle group's lifts must move before the week
    /// counts as progress or as a decline.
    ///
    /// One percent, which sits below the smallest real move a lifter can make and above
    /// pure arithmetic noise: the weight step is 2.5 kg, so a single step is 2.5% of a
    /// 100 kg lift and 5% of a 40 kg one. Anything smaller than a step is rounding, and
    /// between the two thresholds the week is read as flat — no evidence either way, rather
    /// than weak evidence in a direction.
    /// </summary>
    public const decimal StrengthChangeThreshold = 0.01m;

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

        // Oporavak: isti prag u oba smera, jer je ispod celog RIR poena razlika šum
        // procene. Pad snage je treći znak umora, i ulazi ovde a ne u stimulus.
        var showedFatigue = response.AverageRirDeviation <= -MeaningfulRirDeviation
                            || response.FailureShare >= FatigueFailureShare
                            || Declined(response);
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

        // Ciljni volumen: sudi se samo nedelja koja je zaista trenirala blizu cilja, i samo
        // kada ima sa čim da se uporedi. MAV je jedina granica oko koje se stvarno trenira,
        // pa je i jedina o kojoj takva nedelja nešto kaže.
        if (response.PerformedSets >= current.Mav * NearMavShare)
        {
            if (Declined(response))
            {
                // Napredak je stao i snaga pada na volumenu oko cilja: cilj je previsok.
                mav -= MaxWeeklyStep;
            }
            else if (Flat(response))
            {
                // Ni napretka ni pada: stimulus je premali za ovoliko rada.
                mav += MaxWeeklyStep;
            }

            // Napredak znači da cilj radi. Tada se ne dira.
        }

        // Donja granica: sudi se samo kada je nedelja i bila na donjoj granici, jer o
        // minimalnoj dozi ništa ne govori nedelja odrađena znatno iznad nje.
        if (response.PerformedSets <= current.Mev)
        {
            if (Progressed(response))
            {
                // Ovoliko je bilo dovoljno da snaga poraste, pa je minimum niži.
                mev -= MaxWeeklyStep;
            }
            else if (Declined(response))
            {
                // Ovoliko ne održava ni postignuto.
                mev += MaxWeeklyStep;
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

    /// <summary>Week that produced measurable progress in what the muscle's lifts carry.</summary>
    private static bool Progressed(VolumeResponse response)
    {
        return response.StrengthChangeShare >= StrengthChangeThreshold;
    }

    /// <summary>Week that lost measurable strength.</summary>
    private static bool Declined(VolumeResponse response)
    {
        return response.StrengthChangeShare <= -StrengthChangeThreshold;
    }

    /// <summary>
    /// Week that had a comparable measurement and moved less than one weight step either
    /// way. A week with <b>no</b> measurement is not flat — it is silent, and silence
    /// moves nothing.
    /// </summary>
    private static bool Flat(VolumeResponse response)
    {
        return response.StrengthChangeShare is not null
               && !Progressed(response)
               && !Declined(response);
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
