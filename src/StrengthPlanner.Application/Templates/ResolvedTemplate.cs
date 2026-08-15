using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Application.Templates;

/// <summary>
/// Šablon sveden na ono što generator zaista treba: dani, i u svakom danu vežbe onim redom
/// kojim ulaze u trening.
///
/// Postoji da bi generator imao <b>jedan</b> oblik za dve vrste šablona. Ugrađeni šablon je
/// ponuda i tek ovde se skraćuje na nivo iskustva
/// (<see cref="Domain.Algorithms.SessionComposition"/>); lični šablon je propis i prolazi
/// neskraćen.
/// </summary>
public sealed record ResolvedTemplate(
    string Key,
    string Name,
    bool IsCustom,
    IReadOnlyList<ResolvedTemplateDay> Days);

public sealed record ResolvedTemplateDay(
    string Name,
    IReadOnlyList<ResolvedTemplateExercise> Exercises);

/// <summary>
/// Vežba u danu šablona.
///
/// <see cref="Sets"/>, <see cref="RepRangeMin"/> i <see cref="RepRangeMax"/> su <c>null</c>
/// kod ugrađenih šablona - tamo propis dolazi iz cilja i nivoa iskustva. Kod ličnog šablona
/// nose ono što je korisnik uneo, i to je propis <b>prve</b> nedelje: periodizacija ih dalje
/// pomera kroz blok isto kao što pomera propis izveden iz cilja.
/// </summary>
public sealed record ResolvedTemplateExercise(
    Exercise Exercise,
    int? Sets,
    int? RepRangeMin,
    int? RepRangeMax);
