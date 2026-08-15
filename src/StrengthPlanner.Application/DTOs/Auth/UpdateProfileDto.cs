using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Izmena profila ulogovanog korisnika (telesna masa se menja kroz vreme).
/// </summary>
public class UpdateProfileDto
{
    // Opciono polje, pa nema [Required]; [DefinedEnum] propušta null i odbija sve što
    // enum ne definiše.
    [DefinedEnum]
    public Sex? Sex { get; set; }

    [Range(10, 100)]
    public int Age { get; set; }

    [Range(20, 400)]
    public decimal BodyweightKg { get; set; }

    // Bez provere je "experienceLevel": 999 prolazilo, upisivalo se u profil i vraćalo
    // klijentu kao nivo koji nijedan ekran ne prikazuje.
    [Required]
    [DefinedEnum]
    public ExperienceLevel ExperienceLevel { get; set; }
}
