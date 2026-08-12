using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

public class E1RmCalculatorTests
{
    private readonly E1RmCalculator _calculator = new();

    [Fact]
    public void EstimateOneRepMax_UsesEpleyFormula_ForValidRepCount()
    {
        var result = _calculator.EstimateOneRepMax(100m, 1);

        AssertWithinTolerance(103.33m, result, 0.01m);
    }

    [Fact]
    public void EstimateOneRepMax_MatchesDocumentedExample_ForTwelveReps()
    {
        // Primer iz plana: 77.5 kg x 12 -> 108.5 kg.
        var result = _calculator.EstimateOneRepMax(77.5m, 12);

        Assert.Equal(108.5m, result);
    }

    [Fact]
    public void EstimateOneRepMax_CountsRepsInReserveAsEffectiveReps()
    {
        // 80 kg x 8 @ RIR 2 -> efektivnih 10 ponavljanja -> 80 * (1 + 10/30).
        var result = _calculator.EstimateOneRepMax(80m, 8, rir: 2);

        AssertWithinTolerance(106.67m, result, 0.01m);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(20)]
    public void EstimateOneRepMax_Throws_WhenRepsAreAboveEpleyCap(int reps)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _calculator.EstimateOneRepMax(77.5m, reps));

        Assert.Equal("reps", exception.ParamName);
    }

    [Fact]
    public void EstimateOneRepMax_Throws_WhenRirIsNegative()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _calculator.EstimateOneRepMax(77.5m, 8, rir: -1));

        Assert.Equal("rir", exception.ParamName);
    }

    [Fact]
    public void WorkingWeightFor_ReversesEpleyAndRoundsToWeightStep()
    {
        var result = _calculator.WorkingWeightFor(100m, targetReps: 8, targetRir: 1);

        Assert.Equal(77.5m, result);
    }

    [Theory]
    // Sirova težina je 100 / (1 + 9/30) = 76.923 kg; zaokruženje zavisi od koraka vežbe.
    [InlineData(2.5, 77.5)]
    [InlineData(2.0, 76.0)]
    [InlineData(5.0, 75.0)]
    [InlineData(1.0, 77.0)]
    public void WorkingWeightFor_RoundsToSuppliedExerciseStep(double weightStepKg, double expected)
    {
        var result = _calculator.WorkingWeightFor(
            100m,
            targetReps: 8,
            targetRir: 1,
            weightStepKg: (decimal)weightStepKg);

        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void WorkingWeightFor_FallsBackToGlobalStep_WhenStepIsNotSupplied()
    {
        var withoutStep = _calculator.WorkingWeightFor(100m, targetReps: 8, targetRir: 1);
        var withGlobalStep = _calculator.WorkingWeightFor(
            100m,
            targetReps: 8,
            targetRir: 1,
            weightStepKg: TrainingConstants.WeightStepKg);

        Assert.Equal(withGlobalStep, withoutStep);
    }

    private static void AssertWithinTolerance(decimal expected, decimal actual, decimal tolerance)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {actual} to be within {tolerance} of {expected}.");
    }
}
