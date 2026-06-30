using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

public class ProgressionEngineTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    [InlineData(100.0, 12, 3, 12, 3, 12, 3, 1, 8, 12, 107.5, true)]
    [InlineData(100.0, 10, 2, 11, 2, 11, 2, 1, 8, 12, 102.5, false)]
    [InlineData(100.0, 10, 0, 10, 0, 10, 0, 5, 8, 12, 90.0, false)]
    [InlineData(77.5, 12, 2, 12, 2, 12, 3, 1, 8, 12, 82.5, true)]
    public void ComputeNext_AppliesAutoRegulationAndDoubleProgression(
        double usedWeightKg,
        int reps1,
        int rir1,
        int reps2,
        int rir2,
        int reps3,
        int rir3,
        int targetRir,
        int repRangeMin,
        int repRangeMax,
        double expectedNextWeightKg,
        bool expectedWeightIncreased)
    {
        var workingSets = new List<(int reps, int rir)>
        {
            (reps1, rir1),
            (reps2, rir2),
            (reps3, rir3)
        };

        var result = _engine.ComputeNext(
            (decimal)usedWeightKg,
            workingSets,
            targetRir,
            repRangeMin,
            repRangeMax);

        Assert.Equal((decimal)expectedNextWeightKg, result.NextWeightKg);
        Assert.Equal(repRangeMin, result.NextTargetReps);
        Assert.Equal(expectedWeightIncreased, result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_ReturnsInputWeight_WhenWorkingSetsAreEmpty()
    {
        var result = _engine.ComputeNext(
            usedWeightKg: 83.1m,
            workingSets: Array.Empty<(int reps, int rir)>(),
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12);

        Assert.Equal(83.1m, result.NextWeightKg);
        Assert.Equal(8, result.NextTargetReps);
        Assert.False(result.WeightIncreased);
    }
}
