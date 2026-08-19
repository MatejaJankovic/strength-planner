using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;
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

    /// <summary>
    /// Jedina poruka koju prijava vraća pri neuspehu. Nepostojeći nalog, pogrešna lozinka i
    /// zaključan nalog moraju da izgledaju isto, inače je odgovor orakl za nabrajanje naloga.
    /// </summary>
    private const string InvalidCredentials = "Pogrešan email ili lozinka.";

    /// <summary>
    /// Jedina poruka koju registracija vraća pri neuspehu, iz istog razloga:
    /// zauzet email i uhvaćen automat ne smeju da izgledaju različito.
    /// </summary>
    private const string RegistrationFailed = "Registracija nije uspela. Proveri podatke i pokušaj ponovo.";

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        // Zamka za automate: polje je u formularu sakriveno, pa ga čovek ne može popuniti.
        // Odgovor je isti kao za svaki drugi neuspeh, da se ne oda šta je otkrilo automat.
        if (!string.IsNullOrWhiteSpace(dto.Website))
            throw new AuthException(RegistrationFailed);

        // Poruka namerno ne razlikuje zauzet email od ostalih razloga: ranija je bila
        // spisak postojećih naloga za svakoga ko probije redom. Prava zaštita bi bila
        // potvrda email-om, ali dok je nema, bar se ne odgovara na pitanje direktno.
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            throw new AuthException(RegistrationFailed);

        // Profil se kreira zajedno sa nalogom (1:1, isti Guid Id preko FK-a).
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            Profile = new Profile
            {
                DisplayName = dto.DisplayName.Trim(),
                Sex = dto.Sex,
                Age = dto.Age,
                BodyweightKg = dto.BodyweightKg,
                HeightCm = dto.HeightCm,
                ExperienceLevel = dto.ExperienceLevel
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
        {
            // Poruke su iste, ali vreme odgovora nije bilo: nepostojeći nalog se vraćao
            // odmah, a postojeći tek posle PBKDF2 heširanja. Razlika je merljiva i sama
            // po sebi odaje koji email ima nalog, pa se ovde troši isti posao uzalud.
            BurnPasswordHashTime(dto.Password);
            throw new AuthException(InvalidCredentials);
        }

        // Ista poruka kao za pogrešnu lozinku: posebna poruka o zaključavanju je
        // potvrđivala da nalog postoji svakome ko pošalje pet pogrešnih pokušaja.
        if (await _userManager.IsLockedOutAsync(user))
            throw new AuthException(InvalidCredentials);

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Broji neuspešan pokušaj; posle praga Identity zaključava nalog.
            await _userManager.AccessFailedAsync(user);
            throw new AuthException(InvalidCredentials);
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
            DisplayName = profile?.DisplayName,
            Sex = profile?.Sex,
            Age = profile?.Age,
            BodyweightKg = profile?.BodyweightKg,
            HeightCm = profile?.HeightCm,
            ExperienceLevel = profile?.ExperienceLevel,
            HasAvatar = profile?.AvatarBytes != null
        };
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var profile = await RequireProfileAsync(userId);

        // Prazan unos je "nemam ime", a ne ime od nula znakova: bez ovoga bi razmak iz
        // polja postao naslov profila.
        profile.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName)
            ? null
            : dto.DisplayName.Trim();
        profile.Sex = dto.Sex;
        profile.Age = dto.Age;
        profile.BodyweightKg = dto.BodyweightKg;
        profile.HeightCm = dto.HeightCm;
        profile.ExperienceLevel = dto.ExperienceLevel;

        await _db.SaveChangesAsync();

        return await BuildCurrentUserAsync(userId, profile);
    }

    public async Task<CurrentUserDto> SetAvatarAsync(Guid userId, byte[] content)
    {
        if (content.Length == 0)
            throw new AuthException("Slika je prazna.");

        // Granica se proverava i ovde, ne samo u zahtevu: servis je ugovor za sebe, a
        // veličina je jedini razlog zbog kog ovaj upis može da naškodi bazi.
        if (content.Length > ImageFormat.MaximumSizeBytes)
            throw new AuthException(
                $"Slika je veća od {ImageFormat.MaximumSizeBytes / (1024 * 1024)} MB.");

        // Tip se čita iz bajtova. Da se verovalo zaglavlju zahteva, bilo šta poslato kao
        // "image/png" vraćalo bi se posle pregledačima pod tim tipom.
        var contentType = ImageFormat.Detect(content)
            ?? throw new AuthException($"Podržane su samo slike: {ImageFormat.SupportedFormats}.");

        var profile = await RequireProfileAsync(userId);

        profile.AvatarBytes = content;
        profile.AvatarContentType = contentType;

        await _db.SaveChangesAsync();

        return await BuildCurrentUserAsync(userId, profile);
    }

    public async Task<AvatarDto?> GetAvatarAsync(Guid userId)
    {
        // Upit je uvek po vlasniku, kao svaki drugi u ovom servisu; bez toga bi slika bila
        // jedini podatak koji se čita bez provere čiji je.
        var avatar = await _db.Profiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new { profile.AvatarBytes, profile.AvatarContentType })
            .FirstOrDefaultAsync();

        return avatar?.AvatarBytes is { Length: > 0 } content && avatar.AvatarContentType is { } type
            ? new AvatarDto(content, type)
            : null;
    }

    public async Task<CurrentUserDto> RemoveAvatarAsync(Guid userId)
    {
        var profile = await RequireProfileAsync(userId);

        profile.AvatarBytes = null;
        profile.AvatarContentType = null;

        await _db.SaveChangesAsync();

        return await BuildCurrentUserAsync(userId, profile);
    }

    /// <summary>
    /// Profil ulogovanog korisnika, kreiran ako iz nekog razloga ne postoji — isto kao u
    /// <see cref="UpdateProfileAsync"/>, da postavljanje slike ne padne na nalogu bez profila.
    /// </summary>
    private async Task<Profile> RequireProfileAsync(Guid userId)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            _db.Profiles.Add(profile);
        }

        return profile;
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(Guid userId, Profile profile)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthException("Korisnik ne postoji.");

        return new CurrentUserDto
        {
            Id = userId,
            Email = user.Email ?? string.Empty,
            DisplayName = profile.DisplayName,
            Sex = profile.Sex,
            Age = profile.Age,
            BodyweightKg = profile.BodyweightKg,
            HeightCm = profile.HeightCm,
            ExperienceLevel = profile.ExperienceLevel,
            HasAvatar = profile.AvatarBytes != null
        };
    }

    public async Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthException("Korisnik ne postoji.");

        // Bez ovoga je promena lozinke bila orakl za pogađanje trenutne lozinke koji ne
        // troši zaključavanje: onaj ko se domogne tuđeg tokena mogao je da pogađa u
        // nedogled i tako izvuče lozinku u čistom obliku za probanje na drugim sajtovima.
        if (await _userManager.IsLockedOutAsync(user))
            throw new AuthException(InvalidCredentials);

        if (!await _userManager.CheckPasswordAsync(user, dto.CurrentPassword))
        {
            await _userManager.AccessFailedAsync(user);
            throw new AuthException(InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            throw new AuthException(result.Errors.Select(e => e.Description));

        // Identity menja security stamp pri promeni lozinke, pa svi ranije izdati tokeni
        // prestaju da važe. Korisnik dobija nov token da ga izmena ne izbaci iz aplikacije.
        return BuildResponse(user);
    }

    private static string RequireSecurityStamp(ApplicationUser user)
    {
        return string.IsNullOrEmpty(user.SecurityStamp)
            ? throw new AuthException("Nalog nema bezbednosni pečat; prijava nije moguća.")
            : user.SecurityStamp;
    }

    /// <summary>
    /// Heširanje lozinke nad praznim nalogom, samo da bi neuspela prijava trajala isto
    /// koliko i uspela. Rezultat se namerno odbacuje.
    /// </summary>
    private void BurnPasswordHashTime(string password)
    {
        _userManager.PasswordHasher.HashPassword(new ApplicationUser(), password ?? string.Empty);
    }

    private AuthResponseDto BuildResponse(ApplicationUser user)
    {
        var email = user.Email ?? string.Empty;
        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = email,
            // Prazan stamp bi značio token koji prolazi prijavu a pada na svakom
            // narednom zahtevu — bolje odmah pasti, uz jasan razlog.
            Token = _tokenService.CreateToken(user.Id, email, RequireSecurityStamp(user)),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
        };
    }
}
