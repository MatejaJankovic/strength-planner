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

    // Prozor u kome se traži najbolji 1RM za start novog mezociklusa.
    public const int OneRepMaxLookbackDays = 56;

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
}
