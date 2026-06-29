namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Pravi JWT za dati nalog. Prima primitive (ne Identity tip) da bi Application
/// sloj ostao nezavisan od Infrastructure/Identity-ja.
/// </summary>
public interface ITokenService
{
    string CreateToken(Guid userId, string email);
}
