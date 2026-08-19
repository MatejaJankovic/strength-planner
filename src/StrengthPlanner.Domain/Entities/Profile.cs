using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Antropometrijski i trenažni podaci korisnika (1:1 sa <see cref="User"/>).
/// </summary>
public class Profile
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    /// <summary>
    /// Ime kojim korisnik naziva sam sebe; naslov profila umesto email adrese.
    ///
    /// Nullable je iako ga registracija traži, jer nalozi napravljeni pre uvođenja
    /// polja nemaju nikakvo ime i nema odakle da im se izvede. Ekrani koji ga prikazuju
    /// padaju nazad na email kad je prazno.
    /// </summary>
    public string? DisplayName { get; set; }

    public Sex? Sex { get; set; }
    public int Age { get; set; }
    public decimal BodyweightKg { get; set; }

    /// <summary>
    /// Visina u centimetrima. Ne ulazi ni u jedan algoritam — stoji uz pol kao
    /// evidencija, pa je i opciona (nalozi stariji od ovog polja je nemaju).
    /// </summary>
    public decimal? HeightCm { get; set; }

    public ExperienceLevel ExperienceLevel { get; set; }
}
