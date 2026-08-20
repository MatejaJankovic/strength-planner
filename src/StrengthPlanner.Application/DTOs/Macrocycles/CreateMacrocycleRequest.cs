using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Macrocycles;

/// <summary>
/// Pravljenje dugoročnog plana. Prvi blok se generiše odmah; ostali čekaju svoj red,
/// da bi krenuli od 1RM vrednosti koje važe tada.
/// </summary>
public class CreateMacrocycleRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateMacrocycleBlockDto> Blocks { get; set; } = new();
}

public class CreateMacrocycleBlockDto
{
    public Goal Goal { get; set; }

    [Required]
    [MaxLength(64)]
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Model periodizacije za ovaj blok. Bira se po bloku — dugoročan plan i dobija smisao
    /// time što se raspored menja između blokova.
    /// </summary>
    [DefinedEnum]
    public PeriodizationModel PeriodizationModel { get; set; } = PeriodizationModel.Flat;

    /// <summary>
    /// Ko odlučuje o broju serija: ciljni volumen po mišiću, ili propis iz šablona
    /// doslovno. Izostavljeno znači ciljni volumen — zatečeno ponašanje.
    /// </summary>
    [DefinedEnum]
    public SetAllocation SetAllocation { get; set; } = SetAllocation.TargetVolume;
}
