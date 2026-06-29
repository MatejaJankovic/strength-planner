namespace StrengthPlanner.Application.Exceptions;

/// <summary>
/// Greška pri registraciji/prijavi (npr. zauzet email, slaba lozinka, pogrešni kredencijali).
/// API je mapira u 400 Bad Request sa listom poruka.
/// </summary>
public class AuthException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public AuthException(IEnumerable<string> errors) : base("Autentifikacija nije uspela.")
    {
        Errors = errors.ToList();
    }

    public AuthException(string error) : this(new[] { error })
    {
    }
}
