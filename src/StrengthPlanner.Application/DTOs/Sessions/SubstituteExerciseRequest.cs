using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>Zamena vežbe za ostatak bloka.</summary>
public class SubstituteExerciseRequest
{
    [Required]
    public Guid ExerciseId { get; set; }
}
