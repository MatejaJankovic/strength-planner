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

    /// <summary>Menja profil ulogovanog korisnika i vraća ažurirano stanje.</summary>
    Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);

    /// <summary>
    /// Menja lozinku ulogovanog korisnika i vraća nov token. Svi ranije izdati tokeni
    /// prestaju da važe.
    /// </summary>
    Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Postavlja sliku profila. Tip se utvrđuje iz sadržaja, a ne iz zaglavlja zahteva.
    /// </summary>
    /// <exception cref="Exceptions.AuthException">
    /// Sadržaj je prazan, veći od dopuštenog, ili nije podržana slika.
    /// </exception>
    Task<CurrentUserDto> SetAvatarAsync(Guid userId, byte[] content);

    /// <summary>
    /// Vraća sliku profila i njen tip, ili <c>null</c> ako korisnik nema sliku.
    /// </summary>
    Task<AvatarDto?> GetAvatarAsync(Guid userId);

    /// <summary>Uklanja sliku profila.</summary>
    Task<CurrentUserDto> RemoveAvatarAsync(Guid userId);

    /// <summary>
    /// Nepovratno briše nalog i sve podatke u njemu.
    /// </summary>
    /// <exception cref="Exceptions.AuthException">
    /// Lozinka nije ispravna, ili potvrdna reč ne odgovara.
    /// </exception>
    Task DeleteAccountAsync(Guid userId, DeleteAccountDto dto);
}
