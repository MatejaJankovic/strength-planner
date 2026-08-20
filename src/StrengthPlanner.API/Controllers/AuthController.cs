using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.API.Controllers;

/// <summary>
/// Nalog i profil ulogovanog korisnika.
///
/// Nasleđuje <see cref="AuthorizedControllerBase"/> kao i svaki drugi kontroler, pa id
/// korisnika dolazi iz tokena kroz <c>GetUserId()</c> — nigde se ne čita iz tela ni iz
/// query stringa. Registracija i prijava su izuzeci označeni sa <c>[AllowAnonymous]</c>,
/// koji nadjačava <c>[Authorize]</c> sa bazne klase.
///
/// Čitanje <c>sub</c> claim-a je ranije bilo prepisano u svakoj akciji ovog fajla; sa
/// svakim novim endpointom to je bila još jedna kopija istog parsiranja tokena.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : AuthorizedControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("registration")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);
            return Ok(result);
        }
        catch (AuthException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }
        catch (AuthException ex)
        {
            return Unauthorized(new { errors = ex.Errors });
        }
    }

    /// <summary>Zaštićeni test endpoint: vraća trenutnog korisnika iz JWT-a.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();

        var user = await _authService.GetCurrentUserAsync(userId);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Menja lozinku ulogovanog korisnika. Svi ranije izdati tokeni prestaju da važe, pa
    /// se u odgovoru vraća nov — inače bi korisnik promenom lozinke izbacio sam sebe.
    /// </summary>
    [EnableRateLimiting("auth")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var userId = GetUserId();

        try
        {
            return Ok(await _authService.ChangePasswordAsync(userId, dto));
        }
        catch (AuthException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    /// <summary>Menja profil ulogovanog korisnika.</summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        var userId = GetUserId();

        var user = await _authService.UpdateProfileAsync(userId, dto);
        return Ok(user);
    }

    /// <summary>
    /// Postavlja sliku profila.
    ///
    /// Prima multipart, ne base64 u JSON-u: base64 uveća sadržaj za trećinu i tera ceo
    /// zahtev u memoriju kao string pre nego što se veličina uopšte proveri.
    /// </summary>
    [RequestSizeLimit(ImageFormat.MaximumSizeBytes + 4096)]
    [HttpPut("avatar")]
    public async Task<IActionResult> SetAvatar(IFormFile file)
    {
        var userId = GetUserId();

        if (file is null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Slika nije poslata." } });

        // Granica se proverava pre čitanja, da se prevelik sadržaj ne prepiše u memoriju
        // samo da bi posle bio odbijen.
        if (file.Length > ImageFormat.MaximumSizeBytes)
            return BadRequest(new
            {
                errors = new[]
                {
                    $"Slika je veća od {ImageFormat.MaximumSizeBytes / (1024 * 1024)} MB."
                }
            });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        try
        {
            return Ok(await _authService.SetAvatarAsync(userId, buffer.ToArray()));
        }
        catch (AuthException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    /// <summary>Vraća sliku profila ulogovanog korisnika.</summary>
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar()
    {
        var userId = GetUserId();

        var avatar = await _authService.GetAvatarAsync(userId);
        if (avatar is null)
            return NotFound();

        // Tip dolazi iz sadržaja, ne iz onoga što je klijent tvrdio pri otpremanju.
        return File(avatar.Content, avatar.ContentType);
    }

    /// <summary>
    /// Nepovratno briše nalog i sve podatke u njemu.
    ///
    /// Ograničenje broja poziva je isto kao za prijavu i promenu lozinke: zahtev proverava
    /// lozinku, pa je i ovo put kojim se lozinka može pogađati.
    /// </summary>
    [EnableRateLimiting("auth")]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountDto dto)
    {
        var userId = GetUserId();

        try
        {
            await _authService.DeleteAccountAsync(userId, dto);
            return NoContent();
        }
        catch (AuthException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    /// <summary>Uklanja sliku profila ulogovanog korisnika.</summary>
    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var userId = GetUserId();

        try
        {
            return Ok(await _authService.RemoveAvatarAsync(userId));
        }
        catch (AuthException ex)
        {
            // Nalog bez profila: slika ne može ni da postoji, pa ni da se ukloni.
            return BadRequest(new { errors = ex.Errors });
        }
    }
}
