namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Pravi JWT za dati nalog. Prima primitive (ne Identity tip) da bi Application
/// sloj ostao nezavisan od Infrastructure/Identity-ja.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// <paramref name="securityStamp"/> se upisuje kao claim i proverava pri svakom
    /// zahtevu. Identity ga menja pri promeni lozinke, čime ranije izdati tokeni odmah
    /// prestaju da važe — bez toga bi ukradeni token preživeo i promenu lozinke.
    /// </summary>
    string CreateToken(Guid userId, string email, string securityStamp);
}
