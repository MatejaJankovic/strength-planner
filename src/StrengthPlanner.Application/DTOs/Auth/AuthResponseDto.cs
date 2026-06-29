namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Rezultat registracije/prijave — JWT i osnovni podaci naloga.
/// </summary>
public class AuthResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
