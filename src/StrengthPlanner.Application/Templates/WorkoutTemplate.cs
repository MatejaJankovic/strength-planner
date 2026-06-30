namespace StrengthPlanner.Application.Templates;

public sealed record WorkoutTemplate(
    string Key,
    string Name,
    IReadOnlyList<WorkoutTemplateDay> Days);
