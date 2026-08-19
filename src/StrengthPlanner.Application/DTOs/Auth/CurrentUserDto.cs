using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Trenutno prijavljeni korisnik (za GET /api/auth/me).
/// </summary>
public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    // Osnovni profil (može biti null ako iz nekog razloga nije kreiran).
    /// <summary>
    /// Ime korisnika, ili null za naloge napravljene pre uvođenja polja. Ekrani koji ga
    /// prikazuju padaju nazad na <see cref="Email"/>.
    /// </summary>
    public string? DisplayName { get; set; }

    public Sex? Sex { get; set; }
    public int? Age { get; set; }
    public decimal? BodyweightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public ExperienceLevel? ExperienceLevel { get; set; }

    /// <summary>
    /// Da li korisnik ima sliku profila.
    ///
    /// Same bajtove ovaj DTO ne nosi: profil se čita na svakom ekranu, a slika je do dva
    /// megabajta. Klijent po ovoj zastavici zna da li ima šta da traži sa
    /// <c>GET /api/auth/avatar</c>.
    /// </summary>
    public bool HasAvatar { get; set; }
}
