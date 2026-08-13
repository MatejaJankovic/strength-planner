using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class GenerateMesocycleRequest
{
    [Required]
    public string TemplateKey { get; set; } = string.Empty;

    public Goal Goal { get; set; }

    /// <summary>
    /// Kako se propis menja kroz nedelje. Izostavljeno znači ravan blok — ponašanje koje
    /// je sistem imao pre uvođenja modela.
    /// </summary>
    public PeriodizationModel PeriodizationModel { get; set; } = PeriodizationModel.Flat;

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
}
