using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

public class WeightMathTests
{
    [Theory]
    [InlineData(83.1, 2.5, 82.5)]
    [InlineData(81.3, 2.5, 82.5)]
    [InlineData(80.0, 2.5, 80.0)]
    public void RoundToStep_RoundsToNearestWeightStep(double value, double step, double expected)
    {
        var result = WeightMath.RoundToStep((decimal)value, (decimal)step);

        Assert.Equal((decimal)expected, result);
    }

    [Theory]
    [InlineData(55.6, 10.0, 50.0)]
    [InlineData(83.1, 2.5, 82.5)]
    [InlineData(80.0, 2.5, 80.0)]
    [InlineData(22.2, 0.5, 22.0)]
    public void FloorToStep_NeverOvershootsTheValue(double value, double step, double expected)
    {
        // Koristi se tamo gde bi zaokruživanje naviše izmislilo težinu koja nije podignuta:
        // vraćanje reference iz deload težine (NextWeekLoad.UndoDeload).
        var result = WeightMath.FloorToStep((decimal)value, (decimal)step);

        Assert.Equal((decimal)expected, result);
        Assert.True(result <= (decimal)value);
    }

    [Fact]
    public void FloorToStep_RejectsANonPositiveStep()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightMath.FloorToStep(100m, 0m));
    }
}
