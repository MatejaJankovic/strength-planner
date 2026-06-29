namespace StrengthPlanner.Application.DTOs.Exercises;

public class ExerciseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public List<MuscleContributionDto> Muscles { get; set; } = new();
}
