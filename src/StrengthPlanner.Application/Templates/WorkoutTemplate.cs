namespace StrengthPlanner.Application.Templates;

/// <summary>
/// Ugrađeni šablon treninga. <paramref name="Note"/> nosi upozorenje kada šablon ima
/// poznato ograničenje koje korisnik treba da zna pre nego što ga izabere.
/// <paramref name="DayOffsets"/> je sopstveni raspored dana u nedelji (0 = prvi trenažni
/// dan), kada podrazumevani raspored po broju dana stavlja slične složene vežbe u uzastopne
/// dane; <c>null</c> znači podrazumevani (vidi <c>TrainingWeekSchedule</c>).
/// </summary>
public sealed record WorkoutTemplate(
    string Key,
    string Name,
    IReadOnlyList<WorkoutTemplateDay> Days,
    string? Note = null,
    IReadOnlyList<int>? DayOffsets = null);
