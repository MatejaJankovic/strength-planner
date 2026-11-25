namespace StrengthPlanner.Application.Security;

/// <summary>
/// Granice beleške uz vežbu i uz trening, na jednom mestu, kao i <see cref="ProfilePolicy"/>.
///
/// <see cref="MaximumLength"/> mora da prati onu u <c>strength-planner-web</c> (<c>note-field.ts</c>):
/// kad se raziđu, korisnik upiše više nego što server prima i dobije goli 400.
/// </summary>
public static class NotePolicy
{
    /// <summary>Najveća dužina beleške, u karakterima. Beleška, ne dnevnik.</summary>
    public const int MaximumLength = 500;

    /// <summary>
    /// Beleška bez okolnih razmaka; prazna ili samo od razmaka znači „nema beleške" i vraća se
    /// kao null, pa se i čuva kao odsustvo, a ne kao prazan tekst.
    /// </summary>
    public static string? Normalize(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
