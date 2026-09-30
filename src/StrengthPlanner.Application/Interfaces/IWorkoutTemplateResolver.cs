using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Pretvara ključ šablona u sadržaj koji generator koristi.
///
/// Jedno mesto za oba izvora - ugrađeni katalog i lične šablone iz baze - zato što se ključ
/// razrešava na četiri mesta (pravljenje mezociklusa, provera bloka pri pravljenju plana,
/// generisanje bloka kad mu dođe red, i naziv bloka u pregledu). Dok je to bio poziv
/// <c>WorkoutTemplateCatalog.GetByKey</c>, svako novo mesto je moralo da zna i za lične.
/// </summary>
public interface IWorkoutTemplateResolver
{
    /// <summary>
    /// Sadržaj šablona za tog korisnika, ili <c>null</c> ako ključ ne postoji - odnosno ako
    /// lični šablon pripada nekom drugom. Cilj bloka odlučuje koliko složenih vežbi ugrađen
    /// šablon daje po treningu; lični šablon ostaje kakav je.
    /// </summary>
    Task<ResolvedTemplate?> ResolveAsync(
        Guid userId,
        string templateKey,
        Goal goal,
        CancellationToken cancellationToken = default);

    /// <summary>Naziv šablona za prikaz, bez učitavanja vežbi.</summary>
    Task<string?> NameForAsync(
        Guid userId,
        string templateKey,
        CancellationToken cancellationToken = default);
}
