using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.OneRepMax;

/// <summary>
/// A test set the lifter reports, from which the system estimates a one-rep max.
///
/// The thesis promises this next to entering a maximum directly, and it is the only way a
/// bodyweight exercise can be given a starting number at all, since a maximum for one
/// cannot be typed unambiguously.
/// </summary>
public class CreateOneRepMaxFromSetRequest
{
    [Required]
    public Guid ExerciseId { get; set; }

    /// <summary>
    /// What was on the bar, or added on top of body mass. Zero is legitimate for a
    /// bodyweight exercise — the load is the lifter.
    /// </summary>
    [Range(typeof(decimal), "0", "9999", ErrorMessage = "WeightKg cannot be negative.")]
    public decimal WeightKg { get; set; }

    [Range(1, 12, ErrorMessage = "Reps must be between 1 and 12.")]
    public int Reps { get; set; }

    [Range(0, 3, ErrorMessage = "Rir must be between 0 and 3.")]
    public int Rir { get; set; }
}
