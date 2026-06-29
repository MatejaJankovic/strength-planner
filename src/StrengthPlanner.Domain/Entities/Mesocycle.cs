using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Mezociklus — blok treninga (podrazumevano 4 nedelje) sa jednim ciljem.
/// </summary>
public class Mesocycle
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;
    public Goal Goal { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationWeeks { get; set; } = 4;
    public bool IsActive { get; set; }

    public ICollection<TrainingWeek> Weeks { get; set; } = new List<TrainingWeek>();
}
