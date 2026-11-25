using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>Vežba koja se dodaje u dan bloka koji već teče.</summary>
public class AddExerciseRequest
{
    [Required]
    public Guid ExerciseId { get; set; }
}
