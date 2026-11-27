using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>The refresh token of the device that asks to be kept signed in, or to be signed out.</summary>
public class RefreshTokenDto
{
    [Required]
    [MaxLength(RefreshTokenPolicy.MaximumTokenLength)]
    public string RefreshToken { get; set; } = string.Empty;
}
