using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Application.DTOs.Templates;

/// <summary>
/// Lični šablon kakav korisnik pravi ili menja.
///
/// Granice za serije i ponavljanja nisu izmišljene ovde nego preuzete iz
/// <see cref="Periodization"/>. Razlog je praktičan: propis nedelje ionako svodi vrednosti u
/// te opsege, pa bi šire granice ovde značile da korisnik unese broj koji mu plan tiho
/// promeni. Ponavljanja iznad <see cref="Periodization.MaxReps"/> posebno: preko te granice
/// Epley procena ne radi, pa e1RM trend, rekordi i ocena umora ostaju bez podatka.
/// </summary>
public class SaveCustomTemplateRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(TrainingConstants.MaxTemplateDays)]
    public List<SaveCustomTemplateDayDto> Days { get; set; } = new();
}

public class SaveCustomTemplateDayDto
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(TrainingConstants.MaxTemplateExercisesPerDay)]
    public List<SaveCustomTemplateExerciseDto> Exercises { get; set; } = new();
}

public class SaveCustomTemplateExerciseDto
{
    [Required]
    public Guid ExerciseId { get; set; }

    [Range(Periodization.MinSets, TrainingConstants.MaxTemplateSets)]
    public int Sets { get; set; }

    [Range(Periodization.MinReps, Periodization.MaxReps)]
    public int RepRangeMin { get; set; }

    [Range(Periodization.MinReps, Periodization.MaxReps)]
    public int RepRangeMax { get; set; }
}

/// <summary>Lični šablon kakav se vraća klijentu, sa nazivima vežbi za prikaz.</summary>
public sealed class CustomTemplateDto
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<CustomTemplateDayDto> Days { get; set; } = new();
}

public sealed class CustomTemplateDayDto
{
    public string Name { get; set; } = string.Empty;

    public List<CustomTemplateExerciseDto> Exercises { get; set; } = new();
}

public sealed class CustomTemplateExerciseDto
{
    public Guid ExerciseId { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public int Sets { get; set; }

    public int RepRangeMin { get; set; }

    public int RepRangeMax { get; set; }
}
