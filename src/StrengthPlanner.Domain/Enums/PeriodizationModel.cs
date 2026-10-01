namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Kako se propis menja iz nedelje u nedelju unutar bloka.
/// </summary>
public enum PeriodizationModel
{
    /// <summary>
    /// Ravan blok: isti propis svake nedelje, deload na kraju - osim za početnika, kome je i
    /// četvrta nedelja trenažna (Periodization.HasPlannedDeload).
    /// </summary>
    Flat = 0,

    /// <summary>
    /// Klasičan linearan model: od volumena ka intenzitetu - više ponavljanja i serija na
    /// početku, teže i manje na kraju. Ostaje za blokove napravljene pre runde 14; čarobnjak
    /// ga više ne nudi (vidi <see cref="LinearRising"/>). Čuva se kao tekst, pa se ni naziv
    /// ne sme menjati.
    /// </summary>
    Linear = 1,

    /// <summary>Od intenziteta ka volumenu — teško na početku, više ponavljanja na kraju.</summary>
    Inverse = 2,

    /// <summary>
    /// Linearan model iz priručnika: serije rastu kroz blok, a ponavljanja i rezerva padaju -
    /// volumen i intenzitet rastu zajedno ka deload-u. To je i okvir MEV -> MRV koji aplikacija
    /// koristi za granice volumena: nedelja pred deload je najteža, a ne najlakša.
    /// </summary>
    LinearRising = 3
}
