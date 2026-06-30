using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.OneRepMax;

public class CreateOneRepMaxRequest
{
    [Required]
    public Guid ExerciseId { get; set; }

    [Range(typeof(decimal), "0.01", "9999", ErrorMessage = "ValueKg must be greater than zero.")]
    public decimal ValueKg { get; set; }
}
