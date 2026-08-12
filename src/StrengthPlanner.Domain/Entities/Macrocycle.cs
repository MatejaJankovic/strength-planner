namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Dugoročan plan: uređen niz blokova koji se odrađuju jedan za drugim. Svaki blok
/// nosi svoj cilj i šablon, pa se snaga i hipertrofija mogu smenjivati kroz mesece,
/// umesto da svaki mezociklus stoji sam za sebe.
///
/// Pojedinačan mezociklus je makrociklus sa jednim blokom — nema posebnog slučaja.
/// </summary>
public class Macrocycle
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }

    /// <summary>Samo jedan plan je aktivan; pravljenje novog gasi prethodni.</summary>
    public bool IsActive { get; set; }

    public ICollection<MacrocycleBlock> Blocks { get; set; } = new List<MacrocycleBlock>();
}
