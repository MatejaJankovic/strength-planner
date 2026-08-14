using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Podaci za registraciju: nalog + osnovni profil (kreiraju se zajedno).
/// </summary>
public class RegisterDto
{
    // Granica prati Identity kolonu (256). Bez nje je duži email prolazio validaciju i
    // padao tek na upisu, kao 500 umesto 400 — izmereno sa 400 znakova.
    [Required]
    [EmailAddress]
    [MaxLength(EmailPolicy.MaximumLength)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(PasswordPolicy.MinimumLength)]
    [MaxLength(PasswordPolicy.MaximumLength)]
    public string Password { get; set; } = string.Empty;

    // --- osnovni profil ---
    [MaxLength(16)]
    public string? Sex { get; set; }

    [Range(10, 100)]
    public int Age { get; set; }

    [Range(20, 400)]
    public decimal BodyweightKg { get; set; }

    // Bez provere je "experienceLevel": 999 prolazilo, upisivalo se u profil i vraćalo
    // klijentu kao nivo koji nijedan ekran ne prikazuje.
    [Required]
    [DefinedEnum]
    public ExperienceLevel ExperienceLevel { get; set; }

    [Range(1, 7)]
    public int TrainingDaysPerWeek { get; set; }
}
