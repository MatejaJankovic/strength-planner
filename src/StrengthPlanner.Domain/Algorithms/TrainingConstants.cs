namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Shared training algorithm constants used by progression, auto-regulation and e1RM calculations.
/// </summary>
public static class TrainingConstants
{
    public const decimal RpeCorrectionPerPoint = 0.03m;
    public const decimal MaxCorrection = 0.10m;

    // Podrazumevani korak opterećenja kada vežba nema svoj (npr. nepoznata sprava).
    // Stvarni korak po vežbi dolazi iz EquipmentWeightStep ili korisničkog override-a.
    public const decimal WeightStepKg = 2.5m;
    public const decimal DeloadWeightFactor = 0.90m;
    // 12 pokriva ceo hipertrofija rep-opseg (8-12); preko toga Epley procena nije pouzdana.
    public const int EpleyRepCap = 12;

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
