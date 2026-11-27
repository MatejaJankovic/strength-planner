namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Rezultat registracije, prijave i obnavljanja — JWT, token za obnavljanje i osnovni podaci naloga.
/// </summary>
public class AuthResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Kratkoročni <see cref="Token"/> se njime zamenjuje za nov. Menja se pri svakoj upotrebi.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
