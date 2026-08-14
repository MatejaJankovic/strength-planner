using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Application.DTOs.Auth;

public class LoginDto
{
    [Required]
    [EmailAddress]
    [MaxLength(EmailPolicy.MaximumLength)]
    public string Email { get; set; } = string.Empty;

    // Granica i ovde, ne samo pri registraciji: prijava prima isto polje i troši isti
    // posao na njemu, pa bi inače bila put oko ograničenja postavljenog na registraciji.
    [Required]
    [MaxLength(PasswordPolicy.MaximumLength)]
    public string Password { get; set; } = string.Empty;
}
