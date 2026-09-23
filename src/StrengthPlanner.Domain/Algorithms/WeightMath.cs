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

    /// <summary>
    /// Rounds a value down to a multiple of the step, for example 55.6 kg to 50 kg on a
    /// 10 kg step. Used where overshooting would invent a load that was never lifted.
    /// </summary>
    public static decimal FloorToStep(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");
        }

        return Math.Floor(value / step) * step;
    }
}
