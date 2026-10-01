namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Shared training algorithm constants used by progression, auto-regulation and e1RM calculations.
/// </summary>
public static class TrainingConstants
{
    // Korekcija po RIR poenu nije konstanta: izvodi se iz Epley-a za propis vežbe
    // (ProgressionEngine.CorrectionPerRirPoint). Ovo je samo njena granica.
    public const decimal MaxCorrection = 0.10m;

    /// <summary>
    /// How many reps harder than its target reserve a session below the range floor must have
    /// been before a downward correction that rounding erased moves the load down by a whole
    /// step.
    ///
    /// On a light load the step is too coarse to express a correction: 10% of a 10 kg
    /// dumbbell is half its 2 kg step, so the load rounds back to itself and a lifter who
    /// cannot reach the range stays below it. A whole step down is a larger change than the
    /// correction asked for, so it is kept for a session that clearly missed: 5, 4 and 4 reps
    /// at RIR 1, against 8-12 with a target of RIR 1, fall 3.67 reps short and step down; one
    /// set to failure a rep under the floor falls 2 short, and the lifter builds reps at the
    /// same load instead. The rule was first tied to the 10% cap, which the old flat 3% per
    /// point reached at 3.33. Three is a third of a rep looser: the first case it adds is a
    /// set to failure two reps under the floor (6 in 8-12), which used to hold and now steps
    /// down. A session inside the range never steps down, whatever its target reserve.
    /// </summary>
    public const int StepDownRirShortfall = 3;

    // Podrazumevani korak opterećenja kada vežba nema svoj (npr. nepoznata sprava).
    // Stvarni korak po vežbi dolazi iz EquipmentWeightStep ili korisničkog override-a.
    public const decimal WeightStepKg = 2.5m;
    public const decimal DeloadWeightFactor = 0.90m;
    // 12 pokriva ceo hipertrofija rep-opseg (8-12); preko toga Epley procena nije pouzdana.
    public const int EpleyRepCap = 12;

    /// <summary>
    /// Floor of the rep range an isolation exercise is prescribed, in either block.
    ///
    /// Hypertrophy is similar across a wide range of loads when sets end close to failure
    /// (Schoenfeld et al. 2017; 2021), and the handbook itself calls the 8-12 "hypertrophy
    /// range" a myth, adding that the higher range is the usual one for isolation work. Ten,
    /// not eight: at eight reps a lateral raise is already a heavy set for the joint rather
    /// than for the muscle.
    /// </summary>
    public const int IsolationRepRangeMin = 10;

    /// <summary>
    /// Top of the isolation rep range, and the most reps a week may prescribe to one.
    ///
    /// A wider range is also what lets a light dumbbell progress. Double progression steps
    /// the load at the top of the range and starts again at the floor; by Epley, 10-20 at
    /// RIR 1 absorbs about 27% more load, 8-12 about 13%. A 2 kg step on an 8 kg lateral
    /// raise is 25%, so it fits the first range and did not fit the second.
    ///
    /// Above <see cref="EpleyRepCap"/> no e1RM is read, and that stays true: an isolation
    /// set of fifteen is not evidence of a maximum. What the volume limits need from those
    /// sets - whether the lifter got stronger - is read at the same load instead
    /// (<see cref="StrengthChange"/>).
    /// </summary>
    public const int IsolationMaxReps = 20;

    /// <summary>
    /// The divisor of the Epley formula, 1RM = w * (1 + reps / 30). Every rule that trades
    /// reps for load reads it from here, so the estimate, the working weight and the
    /// question of whether a load step fits in a rep range all use the same curve.
    /// </summary>
    public const decimal EpleyRepDivisor = 30m;

    /// <summary>
    /// Most sets one muscle group should get in one session, counted the way weekly
    /// volume is counted (a secondary muscle takes half a set).
    ///
    /// Weekly volume is the target, but it is performed one session at a time, and the
    /// return from one session flattens out: the meta-regressions of Remmert, Pelland,
    /// Robinson, Hinson and Zourdos (2025, preprint) find a positive but diminishing
    /// dose-response per session, with no detectable benefit from sets past about eleven
    /// fractional sets in one session. A set beyond that still costs recovery.
    ///
    /// The handbook's 4-8 sets per session is MAV divided over the week's sessions - where
    /// a session usually lands, not the point past which a set stops working. Enforced as a
    /// ceiling it cuts sets that still measurably help: summed over every built-in week,
    /// 8 removed 3,884.5 sets of weekly muscle volume where 11 removes 985.5.
    /// </summary>
    public const decimal MaxSetsPerMusclePerSession = 11m;

    /// <summary>
    /// Furthest from failure a set may be and still produce an e1RM estimate.
    ///
    /// Defined as <see cref="StimulativeVolume.FullCreditRir"/> on purpose: a set that does
    /// not count as full stimulative volume is not evidence of strength either. Epley
    /// assumes a set to failure, and research on RIR accuracy is consistent that the
    /// estimate degrades the further the lifter stops from it — the thesis says so itself,
    /// and adds that working sets are planned at RIR 1-2.
    /// </summary>
    public const int E1RmMaxRir = StimulativeVolume.FullCreditRir;

    // Prozor u kome se traži najbolji 1RM za start novog mezociklusa.
    public const int OneRepMaxLookbackDays = 56;

    /// <summary>
    /// How far the best estimate in the window may stand above the next best before it is
    /// treated as a single outlier rather than as progress.
    ///
    /// Five percent is chosen below the worst inflation the RIR filter still allows: a set
    /// at RIR 3 reads 150 where the same set to failure reads 140, i.e. about 7%.
    /// </summary>
    public const decimal OneRepMaxOutlierTolerance = 0.05m;

    /// <summary>
    /// How many values the window must hold before the best one may be treated as an outlier.
    ///
    /// With two values there is nothing to corroborate: "best stands more than 5% above the
    /// next" then simply means "take the lower of the two", which is the weak-day problem
    /// that picking the best was there to avoid. A lifter's first two sessions of an
    /// exercise are exactly that window.
    /// </summary>
    public const int OneRepMaxOutlierMinSamples = 3;

    // --- granice ličnog šablona ---
    //
    // Donje granice za serije i ponavljanja NISU ovde: njih već drži Periodization
    // (MinSets, MinReps, MaxReps), pa se odatle i čitaju. Da su prepisane, korisnik bi
    // mogao da unese vrednost koju bi mu propis nedelje tiho pomerio.

    /// <summary>Nedelja ima sedam dana, pa toliko ima i najviše treninga u njoj.</summary>
    public const int MaxTemplateDays = 7;

    /// <summary>
    /// Najviše vežbi u jednom danu. Šest je pun trening i za naprednog vežbača; dvanaest
    /// ostavlja prostora onome ko hoće više, a zaustavlja spisak od sto vežbi.
    /// </summary>
    public const int MaxTemplateExercisesPerDay = 12;

    /// <summary>Najviše serija po vežbi koje šablon sme da propiše.</summary>
    public const int MaxTemplateSets = 10;

    /// <summary>Koliko ličnih šablona jedan nalog sme da drži.</summary>
    public const int MaxTemplatesPerUser = 20;

    /// <summary>
    /// Najveća dužina naziva dana u šablonu.
    ///
    /// Vrednost mora da važi na tri mesta odjednom: u proveri zahteva, u koloni dana
    /// šablona, i u koloni <c>WorkoutSession.DayLabel</c> — jer generator naziv dana
    /// prepisuje u oznaku treninga.
    ///
    /// Bile su dva različita broja: zahtev i šablon su dopuštali 64 znaka, a oznaka
    /// treninga je bila 32. Naziv od 33 do 64 znaka se uredno sačuva kao šablon, a plan
    /// napravljen od njega padne sa 500 na upisu — dakle greška se prijavi na ekranu koji
    /// nema veze sa mestom gde je nastala. Prijavljeno iz stvarne upotrebe.
    /// </summary>
    public const int MaxDayNameLength = 64;
}
