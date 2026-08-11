using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Otkaz proširuje RIR skalu ispod nule i time izjednačava korekciju naniže sa
/// korekcijom naviše, što je ograničenje opisano u zaključku rada.
/// </summary>
public class FailedSetProgressionTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    // Bez otkaza RIR ostaje onakav kakav je unet.
    [InlineData(10, 2, false, 8, 2)]
    // Otkaz na dnu opsega: nije promašeno nijedno ponavljanje -> RIR 0.
    [InlineData(8, 0, true, 8, 0)]
    // Otkaz iznad dna opsega je i dalje 0, ne pozitivan broj.
    [InlineData(11, 0, true, 8, 0)]
    // Otkaz ispod dna opsega: svako promašeno ponavljanje je jedan RIR poen naniže.
    [InlineData(6, 0, true, 8, -2)]
    [InlineData(3, 0, true, 8, -5)]
    public void EffectiveRir_CountsMissedRepsAsNegativeRir(
        int reps,
        int rir,
        bool isFailure,
        int repRangeMin,
        int expected)
    {
        var set = new WorkingSet(reps, rir, isFailure);

        Assert.Equal(expected, set.EffectiveRir(repRangeMin));
    }

    [Fact]
    public void ComputeNext_CorrectsFurtherDown_WhenSetsFailShortOfRange()
    {
        // Tri otkaza na 5 ponavljanja uz opseg 8-12: promašena su po 3 ponavljanja,
        // pa je efektivni RIR -3, odstupanje od cilja -4 poena => -12% ograničeno na -10%.
        var failed = new List<WorkingSet>
        {
            new(5, 0, IsFailure: true),
            new(5, 0, IsFailure: true),
            new(5, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(
            usedWeightKg: 100m,
            failed,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12);

        Assert.Equal(90m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_ReachesSameCapDownwardAsUpward()
    {
        // Simetrija koju rad navodi kao nedostatak: pre otkaza je najveća korekcija
        // naniže bila -3% (RIR 0 uz cilj 1), a naviše punih +10%.
        var tooEasy = new List<WorkingSet> { new(8, 5), new(8, 5), new(8, 5) };
        var tooHard = new List<WorkingSet>
        {
            new(4, 0, IsFailure: true),
            new(4, 0, IsFailure: true),
            new(4, 0, IsFailure: true)
        };

        var up = _engine.ComputeNext(100m, tooEasy, targetRir: 1, repRangeMin: 8, repRangeMax: 12);
        var down = _engine.ComputeNext(100m, tooHard, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(110m, up.NextWeightKg);
        Assert.Equal(90m, down.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_WithoutFailure_KeepsNarrowDownwardCorrection()
    {
        // RIR 0 bez otkaza i dalje znači "jedva sam stigao", ne "nisam uspeo":
        // korekcija ostaje -3%, kao i pre ove izmene.
        var hardButCompleted = new List<WorkingSet> { new(8, 0), new(8, 0), new(8, 0) };

        var result = _engine.ComputeNext(100m, hardButCompleted, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(97.5m, result.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_DoesNotAddWeight_WhenTopOfRangeWasReachedOnlyByFailing()
    {
        // Dvanaest ponavljanja izvučenih do otkaza nije isti signal kao dvanaest sa RIR 1,
        // pa double progression u tom slučaju ne dodaje korak.
        var failedAtTop = new List<WorkingSet>
        {
            new(12, 0, IsFailure: true),
            new(12, 0, IsFailure: true),
            new(12, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(100m, failedAtTop, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.False(result.WeightIncreased);
        Assert.Equal(97.5m, result.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_AveragesFailedAndCompletedSetsTogether()
    {
        // Prva serija prošla sa RIR 2, poslednja otkazala 3 ponavljanja ispod opsega:
        // prosek efektivnog RIR-a je (2 + 0 + (-3)) / 3 = -1/3, odstupanje -4/3 poena.
        var mixed = new List<WorkingSet>
        {
            new(10, 2),
            new(9, 0),
            new(5, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(100m, mixed, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        // -1.3333 * 3% = -4% => 96 kg, zaokruženo na 2.5 kg.
        Assert.Equal(95m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }
}
