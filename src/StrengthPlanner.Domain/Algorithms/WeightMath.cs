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

    /// <summary>
    /// The largest multiple of the step strictly below the value, never below zero: 10 kg
    /// on a 2 kg step gives 8, and 9 kg gives 8 as well. Used where rounding to the nearest
    /// step would land back on the load a rule is meant to move away from.
    /// </summary>
    public static decimal StepBelow(decimal value, decimal step)
    {
        var floored = FloorToStep(value, step);
        var below = floored < value ? floored : floored - step;

        return Math.Max(0m, below);
    }

    /// <summary>
    /// The smallest multiple of the step strictly above the value: 10 kg on a 2 kg step gives
    /// 12, and 15 kg gives 16 - the next load the rack actually has, never more than one
    /// step away. Rounding 15 + 2 to the nearest step would give 18.
    /// </summary>
    public static decimal StepAbove(decimal value, decimal step)
    {
        return FloorToStep(value, step) + step;
    }
}
