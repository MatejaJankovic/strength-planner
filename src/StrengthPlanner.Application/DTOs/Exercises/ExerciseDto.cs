namespace StrengthPlanner.Application.DTOs.Exercises;

public class ExerciseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public bool IsCustom { get; set; }

    /// <summary>Korak opterećenja koji se stvarno primenjuje (korisnički override ili podrazumevani).</summary>
    public decimal WeightStepKg { get; set; }

    /// <summary>Korak izveden iz sprave — vrednost na koju "Vrati podrazumevano" resetuje.</summary>
    public decimal DefaultWeightStepKg { get; set; }

    /// <summary>True kada korisnik ima sopstveni korak za ovu vežbu.</summary>
    public bool IsWeightStepOverridden { get; set; }

    public List<MuscleContributionDto> Muscles { get; set; } = new();
}
