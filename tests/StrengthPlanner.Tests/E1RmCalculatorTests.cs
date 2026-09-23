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

    [Theory]
    // Blizu otkaza: procena važi.
    [InlineData(100.0, 12, 0, true)]
    [InlineData(100.0, 12, 3, true)]
    [InlineData(100.0, 1, 0, true)]
    // Prevelika rezerva: 12 ponavljanja sa RIR 5 čita 156.7 kg tamo gde ista serija do
    // otkaza čita 140 - naduvanih 12% koje je pravilo "najbolja u 56 dana" čuvalo.
    [InlineData(100.0, 12, 4, false)]
    [InlineData(100.0, 12, 5, false)]
    // Iznad Epley granice, kao i do sada.
    [InlineData(100.0, 13, 0, false)]
    // Bez opterećenja nema šta da se skalira (vežbe sa telesnom masom, plank).
    [InlineData(0.0, 8, 1, false)]
    public void CanEstimateFrom_AcceptsOnlySetsThatCanCarryAnEstimate(
        double loadKg,
        int reps,
        int rir,
        bool expected)
    {
        Assert.Equal(expected, E1RmCalculator.CanEstimateFrom((decimal)loadKg, reps, rir));
    }

    [Fact]
    public void CanEstimateFrom_UsesTheStimulativeFullCreditBoundary()
    {
        // Serija koja ne ulazi cela u stimulativni volumen nije ni dokaz o snazi: jedna
        // granica, dva mesta koja je čitaju.
        Assert.Equal(StimulativeVolume.FullCreditRir, TrainingConstants.E1RmMaxRir);
    }

    [Fact]
    public void BestEstimate_IgnoresSetsWithTooMuchReserve()
    {
        // 100 × 12 @RIR5 bi dalo 156.7; ostaje 100 × 10 @RIR1, dakle 100 × (1 + 11/30).
        var best = _calculator.BestEstimate([
            new LoggedSet(100m, 12, 5),
            new LoggedSet(100m, 10, 1)
        ]);

        AssertWithinTolerance(136.67m, best!.Value, 0.01m);
    }

    [Fact]
    public void BestEstimate_ReturnsNull_WhenNoSetQualifies()
    {
        Assert.Null(_calculator.BestEstimate([
            new LoggedSet(100m, 12, 5),
            new LoggedSet(100m, 15, 0),
            new LoggedSet(0m, 8, 1)
        ]));
    }

    [Fact]
    public void BestEstimate_ReturnsNull_ForNoSetsAtAll()
    {
        Assert.Null(_calculator.BestEstimate([]));
    }

    private static void AssertWithinTolerance(decimal expected, decimal actual, decimal tolerance)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {actual} to be within {tolerance} of {expected}.");
    }
}
