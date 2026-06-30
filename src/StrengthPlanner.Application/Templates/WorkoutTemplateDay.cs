namespace StrengthPlanner.Application.Templates;

public sealed record WorkoutTemplateDay(
    string Name,
    IReadOnlyList<string> Exercises);
