using System.Diagnostics.CodeAnalysis;

namespace StrengthPlanner.Application.Templates;

/// <summary>
/// Prevodi ključ šablona u identifikator ličnog šablona i nazad.
///
/// Zahtevi za mezociklus i blok dugoročnog plana nose <c>templateKey</c> kao string i to se
/// ne menja: ugrađeni šabloni zadržavaju svoje ključeve (<c>full-body</c>, <c>upper-lower</c>),
/// a lični dobijaju <c>custom:{guid}</c>. Blok dugoročnog plana se generiše tek kada mu dođe
/// red, pa ključ mora da preživi u bazi i da se tada razreši.
///
/// Prefiks je izabran tako da se ne može sudariti sa ugrađenim ključem: nijedan od njih ne
/// sadrži dvotačku, a katalog to i proverava testom.
/// </summary>
public static class CustomTemplateKey
{
    public const string Prefix = "custom:";

    public static string For(Guid templateId) => Prefix + templateId;

    /// <summary>
    /// Tačno kada je ključ lični šablon. Prefiks bez ispravnog GUID-a nije lični šablon
    /// nego neispravan ključ, pa se ovde vraća <c>false</c> i poziv pada na "nepoznat
    /// šablon" umesto da traži nepostojeći red.
    /// </summary>
    public static bool TryParse(string? templateKey, [NotNullWhen(true)] out Guid templateId)
    {
        templateId = Guid.Empty;

        if (templateKey is null || !templateKey.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(templateKey[Prefix.Length..], out templateId);
    }
}
