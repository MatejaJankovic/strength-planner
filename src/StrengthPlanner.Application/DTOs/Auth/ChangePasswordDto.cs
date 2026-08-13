using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Promena lozinke ulogovanog korisnika. Traži se i stara lozinka: bez nje bi svako ko se
/// domogne tuđeg tokena mogao trajno da preuzme nalog.
/// </summary>
public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(PasswordPolicy.MinimumLength)]
    public string NewPassword { get; set; } = string.Empty;
}
