using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Mezociklus — blok treninga sa jednim ciljem. Trajanje zavisi od modela periodizacije:
/// ravan blok traje 4 nedelje, periodizovani 6.
/// </summary>
public class Mesocycle
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;
    public Goal Goal { get; set; }

    // Kako se propis menja kroz nedelje. Zatečeni blokovi su ravni (0).
    public PeriodizationModel PeriodizationModel { get; set; } = PeriodizationModel.Flat;

    /// <summary>
    /// Ko odlučuje o broju serija. Mora da stoji i ovde, ne samo na bloku: serije se
    /// ponovo balansiraju posle svakog završenog treninga, a tada se blok ne čita.
    /// Zatečeni mezociklusi ciljaju volumen (0).
    /// </summary>
    public SetAllocation SetAllocation { get; set; } = SetAllocation.TargetVolume;

    public DateTime StartDate { get; set; }
    public int DurationWeeks { get; set; } = 4;
    public bool IsActive { get; set; }

    public ICollection<TrainingWeek> Weeks { get; set; } = new List<TrainingWeek>();
}
