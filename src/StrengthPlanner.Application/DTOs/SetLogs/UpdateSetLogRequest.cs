using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.SetLogs;

public class UpdateSetLogRequest
{
    [Range(typeof(decimal), "0", "9999", ErrorMessage = "WeightKg must be greater than or equal to zero.")]
    public decimal WeightKg { get; set; }

    [Range(1, 100)]
    public int Reps { get; set; }

    [Range(0, 5)]
    public int Rir { get; set; }
}
