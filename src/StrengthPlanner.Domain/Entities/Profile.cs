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

    /// <summary>
    /// Slika profila, onako kako je poslata.
    ///
    /// Stoji u koloni baze, a ne na disku: aplikacija radi u kontejneru bez montiranog
    /// volumena, pa bi fajl na disku nestao pri prvom restartu. Uz to, kolona nasleđuje
    /// filtriranje po vlasniku koje svaki upit ovde već ima.
    /// </summary>
    public byte[]? AvatarBytes { get; set; }

    /// <summary>
    /// MIME tip slike, onaj koji je <c>ImageFormat.Detect</c> utvrdio iz bajtova.
    ///
    /// Namerno se ne pamti ono što je klijent poslao u zaglavlju: taj tip se vraća svakom
    /// pregledaču koji otvori profil, pa mora da bude tvrdnja servera, a ne klijenta.
    /// </summary>
    public string? AvatarContentType { get; set; }

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
