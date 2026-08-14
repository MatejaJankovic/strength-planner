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
        // Prazna vrednost je briga [Required] atributa, ne ovog. Ovde se završava i
        // nullable enum bez vrednosti: CLR pakuje Nullable<T> kao null ili kao T, pa
        // sve što stigne dalje već jeste sama enum vrednost.
        if (value is null)
        {
            return true;
        }

        var enumType = value.GetType();
        if (!enumType.IsEnum)
        {
            throw new InvalidOperationException(
                $"[DefinedEnum] je stavljen na {enumType.Name}, a radi samo nad enum tipom.");
        }

        // Enum.IsDefined ne prepoznaje kombinacije bit-zastavica kao ispravne, pa bi ovakav
        // enum tiho odbijao ispravan unos. Trenutno ga u projektu nema; ako se pojavi, neka
        // to bude greška u programiranju umesto 400 koji niko ne ume da objasni.
        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            throw new InvalidOperationException(
                $"[DefinedEnum] ne ume da proveri [Flags] enum {enumType.Name}: Enum.IsDefined "
                + "odbija ispravne kombinacije zastavica.");
        }

        return Enum.IsDefined(enumType, value);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} nije jedna od dozvoljenih vrednosti.";
    }
}
