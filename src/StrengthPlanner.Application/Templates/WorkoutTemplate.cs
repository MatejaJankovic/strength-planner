namespace StrengthPlanner.Application.Templates;

/// <summary>
/// Ugrađeni šablon treninga. <paramref name="Note"/> nosi upozorenje kada šablon ima
/// poznato ograničenje koje korisnik treba da zna pre nego što ga izabere.
/// </summary>
public sealed record WorkoutTemplate(
    string Key,
    string Name,
    IReadOnlyList<WorkoutTemplateDay> Days,
    string? Note = null);
