namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Identitet korisnika koji je poslao zahtev koji se trenutno obrađuje.
///
/// Postoji da bi sloj podataka mogao sam da ograniči redove na vlasnika, umesto da se
/// oslanja isključivo na to da je svaki servis zapamtio da doda uslov po <c>UserId</c>.
/// Vrednost se čita iz <c>sub</c> claim-a validiranog tokena i nikada iz tela zahteva,
/// rute ili query stringa.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Identifikator prijavljenog korisnika, ili <c>null</c> kada zahteva nema
    /// (migracije i seed pri pokretanju) ili je anoniman (prijava, registracija).
    /// </summary>
    Guid? UserId { get; }
}
