namespace StrengthPlanner.Application.Security;

/// <summary>
/// Granice email adrese, na jednom mestu — kao i <see cref="PasswordPolicy"/>.
/// </summary>
public static class EmailPolicy
{
    /// <summary>
    /// Najveća dužina email adrese.
    ///
    /// Vrednost prati kolonu koju Identity pravi za <c>Email</c> i <c>NormalizedEmail</c>
    /// (256 znakova). Bez ove granice je duža adresa prolazila validaciju i padala tek pri
    /// upisu u bazu — izmereno: adresa od 400 znakova vraćala je 500 umesto 400, dakle
    /// grešku servera umesto poruke da je unos neispravan.
    /// </summary>
    public const int MaximumLength = 256;
}
