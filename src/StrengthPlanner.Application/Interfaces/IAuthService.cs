using StrengthPlanner.Application.DTOs.Auth;

namespace StrengthPlanner.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Kreira nalog + profil i vraća JWT.</summary>
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);

    /// <summary>Proverava kredencijale i vraća JWT.</summary>
    Task<AuthResponseDto> LoginAsync(LoginDto dto);

    /// <summary>Vraća trenutnog korisnika po Id-u iz tokena (null ako ne postoji).</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId);
}
