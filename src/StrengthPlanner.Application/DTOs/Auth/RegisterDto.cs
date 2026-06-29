using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Podaci za registraciju: nalog + osnovni profil (kreiraju se zajedno).
/// </summary>
public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    // --- osnovni profil ---
    [MaxLength(16)]
    public string? Sex { get; set; }

    [Range(10, 100)]
    public int Age { get; set; }

    [Range(20, 400)]
    public decimal BodyweightKg { get; set; }

    [Required]
    public ExperienceLevel ExperienceLevel { get; set; }

    [Range(1, 7)]
    public int TrainingDaysPerWeek { get; set; }
}
