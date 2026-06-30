namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Weight rounding helpers for kilogram-based loading.
/// </summary>
public static class WeightMath
{
    /// <summary>
    /// Rounds a value to the nearest multiple of the provided step, for example 83.1 kg to 82.5 kg.
    /// </summary>
    public static decimal RoundToStep(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");
        }

        return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
    }
}
