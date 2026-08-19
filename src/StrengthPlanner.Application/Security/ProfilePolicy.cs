namespace StrengthPlanner.Application.Security;

/// <summary>
/// Granice profilnih polja, na jednom mestu — kao i <see cref="EmailPolicy"/>.
///
/// Vrednosti ovde moraju da prate one u <c>strength-planner-web</c>
/// (<c>auth.models.ts</c>). Kad se raziđu, korisnik prođe svaku proveru u pregledaču pa
/// dobije goli 400 sa servera, bez označenog polja — isto što se već desilo sa dužinom
/// lozinke.
/// </summary>
public static class ProfilePolicy
{
    /// <summary>Najveća dužina imena koje korisnik sam upisuje.</summary>
    public const int DisplayNameMaximumLength = 64;

    /// <summary>Najmanja visina koja se prihvata, u centimetrima.</summary>
    public const double MinimumHeightCm = 100;

    /// <summary>Najveća visina koja se prihvata, u centimetrima.</summary>
    public const double MaximumHeightCm = 250;
}
