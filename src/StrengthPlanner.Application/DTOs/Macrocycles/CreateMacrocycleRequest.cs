using System.ComponentModel.DataAnnotations;
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
}
