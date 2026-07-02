using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Authentication;

/// <summary>
/// Implementacija auth use-case-ova. Živi u Infrastructure jer zavisi od
/// Identity <see cref="UserManager{TUser}"/>; ugovor (IAuthService, DTO-ovi) je u Application.
/// Lozinke hešira Identity (UserManager), ne pišemo sopstveno heširanje.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        AppDbContext db,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            throw new AuthException("Nalog sa datim email-om već postoji.");

        // Profil se kreira zajedno sa nalogom (1:1, isti Guid Id preko FK-a).
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            Profile = new Profile
            {
                Sex = dto.Sex,
                Age = dto.Age,
                BodyweightKg = dto.BodyweightKg,
                ExperienceLevel = dto.ExperienceLevel,
                TrainingDaysPerWeek = dto.TrainingDaysPerWeek
            }
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new AuthException(result.Errors.Select(e => e.Description));

        return BuildResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            throw new AuthException("Pogrešan email ili lozinka.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new AuthException("Nalog je privremeno zaključan zbog previše neuspešnih pokušaja. Pokušaj ponovo za nekoliko minuta.");

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Broji neuspešan pokušaj; posle praga Identity zaključava nalog.
            await _userManager.AccessFailedAsync(user);
            throw new AuthException("Pogrešan email ili lozinka.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return BuildResponse(user);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        // Profil ima PK = UserId.
        var profile = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Sex = profile?.Sex,
            Age = profile?.Age,
            BodyweightKg = profile?.BodyweightKg,
            ExperienceLevel = profile?.ExperienceLevel,
            TrainingDaysPerWeek = profile?.TrainingDaysPerWeek
        };
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            _db.Profiles.Add(profile);
        }

        profile.Sex = dto.Sex;
        profile.Age = dto.Age;
        profile.BodyweightKg = dto.BodyweightKg;
        profile.ExperienceLevel = dto.ExperienceLevel;
        profile.TrainingDaysPerWeek = dto.TrainingDaysPerWeek;

        await _db.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthException("Korisnik ne postoji.");

        return new CurrentUserDto
        {
            Id = userId,
            Email = user.Email ?? string.Empty,
            Sex = profile.Sex,
            Age = profile.Age,
            BodyweightKg = profile.BodyweightKg,
            ExperienceLevel = profile.ExperienceLevel,
            TrainingDaysPerWeek = profile.TrainingDaysPerWeek
        };
    }

    private AuthResponseDto BuildResponse(ApplicationUser user)
    {
        var email = user.Email ?? string.Empty;
        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = email,
            Token = _tokenService.CreateToken(user.Id, email),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
        };
    }
}
