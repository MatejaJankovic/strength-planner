namespace StrengthPlanner.Application.Security;

/// <summary>
/// Pravila potvrde brisanja naloga, na jednom mestu — kao i ostale politike ovde.
/// </summary>
public static class AccountDeletionPolicy
{
    /// <summary>
    /// Reč koju korisnik mora da otkuca da bi nalog bio obrisan.
    ///
    /// Na srpskom je, jer je i ceo interfejs na srpskom: reč koju korisnik prepisuje mora
    /// da bude reč koju na ekranu i čita. Mora da prati istu vrednost u
    /// <c>auth.models.ts</c>; kada se raziđu, ekran traži jedno a server drugo i brisanje
    /// prestaje da radi bez ijedne poruke o tome zašto.
    /// </summary>
    public const string ConfirmationWord = "OBRIŠI";
}
