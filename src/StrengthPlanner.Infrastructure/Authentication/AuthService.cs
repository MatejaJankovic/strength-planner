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
        var profile = await CreateProfileIfMissingAsync(userId);

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

        var profile = await RequireExistingProfileAsync(userId);

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
        var profile = await RequireExistingProfileAsync(userId);

        profile.AvatarBytes = null;
        profile.AvatarContentType = null;

        await _db.SaveChangesAsync();

        return await BuildCurrentUserAsync(userId, profile);
    }

    /// <summary>
    /// Profil ulogovanog korisnika, kreiran prazan ako ne postoji.
    ///
    /// Smeju da ga zovu samo pozivaoci koji odmah upisuju **sva** polja profila —
    /// praktično samo <see cref="UpdateProfileAsync"/>. Prazan profil ima
    /// <c>Age = 0</c> i <c>BodyweightKg = 0</c>, jer su ta polja u entitetu ne-nullable;
    /// ko ih ne prepiše, upisao je profil koji tvrdi da vežbač ima nula godina i nula
    /// kilograma, i ekran profila bi to i prikazao.
    /// </summary>
    private async Task<Profile> CreateProfileIfMissingAsync(Guid userId)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            _db.Profiles.Add(profile);
        }

        return profile;
    }

    /// <summary>
    /// Profil ulogovanog korisnika; greška ako ga nema.
    ///
    /// Slika se ne može postaviti pre profila. Registracija profil uvek kreira, pa je
    /// njegov nedostatak neispravno stanje naloga, a ne slučaj koji treba popuniti
    /// podrazumevanim vrednostima — kreiranje ovde bi upisalo uzrast 0 i masu 0 samo zato
    /// što je korisnik izabrao sliku.
    /// </summary>
    private async Task<Profile> RequireExistingProfileAsync(Guid userId)
    {
        return await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId)
            ?? throw new AuthException("Profil ne postoji. Sačuvaj osnovne podatke pre slike.");
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

    /// <summary>
    /// Briše nalog i sve što mu pripada, u jednoj transakciji.
    ///
    /// Red poslova nije stvar ukusa, nego posledica stranih ključeva:
    ///
    /// 1. <b>Lični šabloni</b> idu prvi. Njihove stavke drže vežbe preko
    ///    <c>UserWorkoutTemplateExercise → Exercise</c> sa <c>Restrict</c>, pa dok šablon
    ///    postoji, korisnikova sopstvena vežba se ne može obrisati.
    /// 2. <b>Nalog</b> ide drugi. Kaskade iz <c>ApplicationUserConfiguration</c> odnose
    ///    profil, mezocikluse (a s njima nedelje, treninge, planove vežbi i serije),
    ///    maksimume, podešavanja vežbi, orijentire volumena i dugoročne planove. Time se
    ///    puštaju i <c>Restrict</c> veze koje <c>ExercisePlan</c> i <c>OneRepMaxRecord</c>
    ///    drže na vežbama.
    /// 3. <b>Sopstvene vežbe</b> idu poslednje, kada ih ništa više ne referiše.
    ///
    /// Transakcija je tu zato što ovo nije jedan upis: da treći korak padne bez nje, nalog
    /// bi bio obrisan a njegove vežbe bi ostale u katalogu bez vlasnika.
    ///
    /// <c>UserWorkoutTemplate</c> i <c>Exercise.CreatedByUserId</c> nose samo Guid, bez
    /// stranog ključa ka nalogu — zato ih kaskada ne dohvata i zato se brišu ručno.
    /// <c>AccountDeletionTests</c> to drži: novi korisnički entitet koji nije ni u kaskadi
    /// ni u ovom spisku obara build.
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, DeleteAccountDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthException("Korisnik ne postoji.");

        // Potvrdna reč se proverava PRE lozinke, i to je bezbednosna odluka, ne stilska.
        //
        // Dve provere daju dve različite poruke. Da lozinka ide prva, ko se domogne tuđeg
        // tokena mogao bi da pogađa lozinku sa namerno pogrešnom rečju: „pogrešan email
        // ili lozinka" znači da pogodak nije, a poruka o potvrdnoj reči znači da jeste — a
        // nalog se pri tome ne briše. Ovako pogrešna reč ne odaje ništa o lozinci, i
        // skupo heširanje se ne troši na zahtev koji ne može da uspe.
        //
        // Poredi se ordinalno, bez obzira na velika i mala slova. Kultura ovde ne sme da
        // učestvuje: na hostu sa turskim jezikom „i" i „I" nisu isto slovo, pa bi
        // `CurrentCultureIgnoreCase` odbijao „obriši" prema „OBRIŠI" — reč se završava
        // upravo tim slovom — i nalog se ne bi mogao obrisati, uz poruku da otkuca ono što
        // je već otkucao. Kultura nigde nije zakucana (ni u Program.cs, ni u csproj-u, ni
        // u Dockerfile-u), pa je to bila stvar hosta. Ordinalno poređenje ispravno sabija
        // š i Š i ne zavisi od jezika.
        if (!string.Equals(
                dto.Confirmation.Trim(),
                AccountDeletionPolicy.ConfirmationWord,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(
                $"Za brisanje naloga otkucaj \"{AccountDeletionPolicy.ConfirmationWord}\".");
        }

        // Ista zaštita kao pri promeni lozinke: bez nje je brisanje naloga bilo orakl za
        // pogađanje lozinke koji ne troši zaključavanje.
        if (await _userManager.IsLockedOutAsync(user))
            throw new AuthException(InvalidCredentials);

        if (!await _userManager.CheckPasswordAsync(user, dto.CurrentPassword))
        {
            await _userManager.AccessFailedAsync(user);
            throw new AuthException(InvalidCredentials);
        }

        // Kao i u LoginAsync i ChangePasswordAsync: ispravna lozinka briše ranije neuspele
        // pokušaje. Bez ovoga dve greške u kucanju na ovom ekranu ostaju na nalogu i
        // kasnija obična greška pri prijavi ga zaključava.
        await _userManager.ResetAccessFailedCountAsync(user);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // `ExecuteDeleteAsync` šalje jedan DELETE po skupu i ne uvlači redove u praćenje
        // promena samo da bi ih označio za brisanje. Kaskade i filtere vlasništva poštuje
        // kao i svaki drugi upit, a transakcija ostaje što kraća.
        await _db.UserWorkoutTemplates
            .Where(template => template.UserId == userId)
            .ExecuteDeleteAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new AuthException(result.Errors.Select(e => e.Description));

        await _db.Exercises
            .Where(exercise => exercise.CreatedByUserId == userId)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
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
