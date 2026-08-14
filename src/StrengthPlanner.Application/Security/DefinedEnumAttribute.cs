using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.Security;

/// <summary>
/// Traži da vrednost bude jedna od onih koje enum stvarno definiše.
///
/// Bez ovoga model binder prima bilo koji ceo broj: <c>experienceLevel: 999</c> je prolazio
/// validaciju, upisivao se u profil i vraćao klijentu kao nivo koji nijedan ekran ne ume da
/// prikaže, a algoritmi bi na njega odgovarali svojom podrazumevanom granom. Provera je
/// ista ona koju <c>MacrocycleService</c> već piše ručno za cilj i model periodizacije;
/// ovde je kao atribut, da svaki nov enum u zahtevu ne mora da je ponovo izmišlja.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DefinedEnumAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        // Prazna vrednost je briga [Required] atributa, ne ovog.
        if (value is null)
        {
            return true;
        }

        var type = value.GetType();
        var enumType = Nullable.GetUnderlyingType(type) ?? type;

        return enumType.IsEnum && Enum.IsDefined(enumType, value);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} nije jedna od dozvoljenih vrednosti.";
    }
}
