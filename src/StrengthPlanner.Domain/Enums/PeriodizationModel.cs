namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Kako se propis menja iz nedelje u nedelju unutar bloka.
/// </summary>
public enum PeriodizationModel
{
    /// <summary>Ravan blok: isti propis svake nedelje, deload na kraju.</summary>
    Flat = 0,

    /// <summary>Od volumena ka intenzitetu — više ponavljanja na početku, teže na kraju.</summary>
    Linear = 1,

    /// <summary>Od intenziteta ka volumenu — teško na početku, više ponavljanja na kraju.</summary>
    Inverse = 2
}
