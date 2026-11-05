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
    /// How much the estimated strength of a muscle group's lifts must move before a reading
    /// counts as progress or as a decline.
    ///
    /// One percent, which sits below the smallest real move a lifter can make and above
    /// pure arithmetic noise: the weight step is 2.5 kg, so a single step is 2.5% of a
    /// 100 kg lift and 5% of a 40 kg one.
    ///
    /// The volume limits no longer use it: they judge the mean of two readings against
    /// <see cref="TwoWeekChangeThreshold"/>. It stays as the fatigue score's test of whether
    /// the previous comparable week also declined (FatigueEvaluator, round 15).
    /// </summary>
    public const decimal StrengthChangeThreshold = 0.01m;

    /// <summary>
    /// How far the mean of two consecutive comparable readings must move before the limits
    /// read it as progress or as a decline; anything closer to zero is flat.
    ///
    /// The mean of two consecutive readings is half of the change across two weeks, because
    /// the week in the middle cancels out, so its noise is about half that of one reading
    /// (sd about 1.75% against 3.5% in the model of docs/simulations/landmark_signal_noise.py).
    /// 2.5% is the threshold at which it gives as few false declines as the rule it replaced
    /// (two readings that each had to cross 1%) - and, measured over real block structure,
    /// it then catches a real decline better, decides about four times as often, and lets
    /// a lifter who has stalled read as flat, so the target rises as the table of rules says.
    /// See <see cref="TwoWeekChange"/>.
    /// </summary>
    public const decimal TwoWeekChangeThreshold = 0.025m;

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

    /// <summary>Two comparable weeks whose mean reading shows measurable progress.</summary>
    private static bool Progressed(VolumeResponse response)
    {
        return TwoWeekChange(response) >= TwoWeekChangeThreshold;
    }

    /// <summary>Two comparable weeks whose mean reading shows a measurable decline.</summary>
    private static bool Declined(VolumeResponse response)
    {
        return TwoWeekChange(response) <= -TwoWeekChangeThreshold;
    }

    /// <summary>
    /// Two comparable weeks whose mean reading moved less than the threshold either way. A
    /// week with <b>no</b> pair of readings is not flat — it is silent, and silence moves
    /// nothing.
    /// </summary>
    private static bool Flat(VolumeResponse response)
    {
        return TwoWeekChange(response) is { } change && Math.Abs(change) < TwoWeekChangeThreshold;
    }

    /// <summary>
    /// The mean of this week's strength reading and the previous comparable week's, or null
    /// when either is missing.
    ///
    /// One reading is mostly noise. Misjudging the reserve by one rep moves an e1RM by
    /// 1 / (30 + effective reps), about 2.4% for a set of 8-12 at RIR 1-2; lifters do misjudge
    /// it, on average by underpredicting about one rep and with wide heterogeneity (Halperin et
    /// al. 2022), and a one-rep max itself varies day to day by a median 4.2% (Grgic et al.
    /// 2020). How much a lifter's own misjudgement varies from week to week is a modelling
    /// assumption, not a figure from either paper: the simulation gives one reading an sd of
    /// about 3.5%, while true progress is well under 1% a week.
    ///
    /// Round 14 (#94) first required both readings to cross 1% in the same direction. The
    /// review of #94 re-measured it with the block structure the app really has - a reading
    /// only from the second week of a block, none in a deload, a fresh start each block - and
    /// that rule decided about three times a year in a flat block, let a stalled lifter drift
    /// down, and caught a real decline weakly. The mean at 2.5% keeps the same low rate of
    /// false declines and fixes the other three (docs/features/landmark-signal-noise.md).
    ///
    /// The fatigue score uses its own confirmation: a strength drop counts only when the
    /// previous comparable reading also fell past <see cref="StrengthChangeThreshold"/>
    /// (FatigueEvaluator, round 15).
    /// </summary>
    private static decimal? TwoWeekChange(VolumeResponse response)
    {
        return response.StrengthChangeShare is { } current
               && response.PreviousStrengthChangeShare is { } previous
            ? (current + previous) / 2
            : null;
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
