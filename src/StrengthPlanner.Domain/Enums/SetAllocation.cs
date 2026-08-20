namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Ko odlučuje o broju serija u bloku.
///
/// Od runde 5 planer bira serije tako da nedelja padne u ciljnu zonu volumena svakog
/// mišića (MAV). To je tačno ono što treba kada šablon dolazi iz kataloga: ugrađeni
/// šabloni ne nose nameru o volumenu, nego raspored vežbi.
///
/// Kod ličnog šablona nije tako. Korisnik koji otkuca „3 serije" izgovorio je nameru, a
/// balansiranje ju je tiho menjalo — prijavljeno iz stvarne upotrebe: uneto 3, plan
/// propisao 5, i nigde nije pisalo zašto. Zato je izbor sada njegov, i bira se po bloku.
/// </summary>
public enum SetAllocation
{
    /// <summary>
    /// Serije se prilagođavaju ciljnom volumenu po mišiću. Zatečeno ponašanje, i dalje
    /// podrazumevano — nulta vrednost, pa svi postojeći blokovi ostaju kakvi jesu.
    /// </summary>
    TargetVolume = 0,

    /// <summary>
    /// Serije ostaju onakve kakve ih propis daje. Nedeljni volumen tada može da ostane
    /// ispod ciljne zone, i sistem ga neće ispravljati — to je i smisao ovog izbora.
    /// </summary>
    FollowTemplate = 1
}
