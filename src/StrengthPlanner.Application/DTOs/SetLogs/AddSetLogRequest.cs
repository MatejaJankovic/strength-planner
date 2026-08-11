using System.ComponentModel.DataAnnotations;

namespace StrengthPlanner.Application.DTOs.SetLogs;

public class AddSetLogRequest
{
    [Range(typeof(decimal), "0", "9999", ErrorMessage = "WeightKg must be greater than or equal to zero.")]
    public decimal WeightKg { get; set; }

    [Range(1, 100)]
    public int Reps { get; set; }

    [Range(0, 5)]
    public int Rir { get; set; }

    /// <summary>
    /// Serija izvučena do otkaza. Kada je true, RIR se ignoriše i upisuje kao 0 —
    /// otkaz po definiciji znači da nije ostalo nijedno ponavljanje u rezervi.
    /// </summary>
    public bool IsFailure { get; set; }
}
