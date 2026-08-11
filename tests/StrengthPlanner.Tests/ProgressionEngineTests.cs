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
        var workingSets = new List<WorkingSet>
        {
            new(reps1, rir1),
            new(reps2, rir2),
            new(reps3, rir3)
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

    [Theory]
    // Dumbbell (2 kg): 20 kg + korak, jer su sve serije stigle do vrha opsega.
    [InlineData(20.0, 2.0, 22.0)]
    // Machine (5 kg): isti unos skače za ceo stack korak i zaokružuje se na njega.
    [InlineData(20.0, 5.0, 25.0)]
    // Barbell (2.5 kg): referentno ponašanje pre uvođenja koraka po vežbi.
    [InlineData(20.0, 2.5, 22.5)]
    public void ComputeNext_UsesExerciseWeightStepForIncrementAndRounding(
        double usedWeightKg,
        double weightStepKg,
        double expectedNextWeightKg)
    {
        var workingSets = new List<WorkingSet> { new(12, 1), new(12, 1), new(12, 1) };

        var result = _engine.ComputeNext(
            (decimal)usedWeightKg,
            workingSets,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: (decimal)weightStepKg);

        Assert.Equal((decimal)expectedNextWeightKg, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_RoundsDownwardCorrectionToExerciseStep()
    {
        // Prosečan RIR 0 uz cilj 2 => -6%: 40 kg -> 37.6 kg, zaokruženo na korak od 2 kg.
        var workingSets = new List<WorkingSet> { new(10, 0), new(10, 0), new(10, 0) };

        var result = _engine.ComputeNext(
            usedWeightKg: 40m,
            workingSets,
            targetRir: 2,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2m);

        Assert.Equal(38m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_DefaultsToGlobalStep_WhenExerciseStepIsNotSupplied()
    {
        var workingSets = new List<WorkingSet> { new(12, 1), new(12, 1), new(12, 1) };

        var withoutStep = _engine.ComputeNext(100m, workingSets, 1, 8, 12);
        var withGlobalStep = _engine.ComputeNext(100m, workingSets, 1, 8, 12, TrainingConstants.WeightStepKg);

        Assert.Equal(withGlobalStep.NextWeightKg, withoutStep.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_ReturnsInputWeight_WhenWorkingSetsAreEmpty()
    {
        var result = _engine.ComputeNext(
            usedWeightKg: 83.1m,
            workingSets: Array.Empty<WorkingSet>(),
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12);

        Assert.Equal(83.1m, result.NextWeightKg);
        Assert.Equal(8, result.NextTargetReps);
        Assert.False(result.WeightIncreased);
    }
}
