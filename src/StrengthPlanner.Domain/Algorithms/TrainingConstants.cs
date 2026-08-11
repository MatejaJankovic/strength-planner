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
}
