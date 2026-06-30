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
}
