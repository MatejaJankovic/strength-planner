using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Od koje težine progresija polazi kad serije nisu sve na istoj težini.
///
/// Do sada je polazila od **proseka** svih upisanih težina, pa je jedna lakša serija
/// (back-off, ili zagrevanje upisano kao radna serija) spuštala predlog ispod težine koju je
/// vežbač u istom treningu podigao dva puta.
/// </summary>
public class WorkingLoadTests
{
    private readonly ProgressionEngine _engine = new();

    [Fact]
    public void Select_ReturnsNull_WhenNothingWasLogged()
    {
        Assert.Null(WorkingLoad.Select([]));
    }

    [Fact]
    public void Select_UsesTheHeaviestLoadAsReference()
    {
        var load = WorkingLoad.Select([
            new LoggedSet(100m, 12, 1),
            new LoggedSet(100m, 12, 1),
            new LoggedSet(80m, 12, 1)
        ]);

        Assert.NotNull(load);
        Assert.Equal(100m, load!.ReferenceWeightKg);
        // Lakša serija sa rezervom ne govori o 100 kg: njen RIR je izmeren na drugoj težini.
        Assert.Equal(2, load.WorkingSets.Count);
        Assert.Equal(1, load.ExcludedLighterSets);
    }

    [Fact]
    public void Select_KeepsALighterSetThatEndedWithoutReserve()
    {
        // Otkaz na 90 kg znači otkaz najkasnije na 100 kg, pa takva serija jeste dokaz o
        // referentnoj težini - i to dokaz koji korekciju može samo da povuče naniže.
        var load = WorkingLoad.Select([
            new LoggedSet(100m, 12, 1),
            new LoggedSet(90m, 10, 0, IsFailure: true),
            new LoggedSet(90m, 12, 2)
        ]);

        Assert.NotNull(load);
        Assert.Equal(100m, load!.ReferenceWeightKg);
        Assert.Equal(2, load.WorkingSets.Count);
        Assert.Contains(load.WorkingSets, set => set.Reps == 10 && set.IsFailure);
        Assert.Equal(1, load.ExcludedLighterSets);
    }

    [Fact]
    public void Select_PassesEverySetThrough_WhenTheyShareOneLoad()
    {
        var load = WorkingLoad.Select([
            new LoggedSet(100m, 12, 1),
            new LoggedSet(100m, 10, 0),
            new LoggedSet(100m, 8, 2)
        ]);

        Assert.NotNull(load);
        Assert.Equal(100m, load!.ReferenceWeightKg);
        Assert.Equal(3, load.WorkingSets.Count);
        Assert.Equal(0, load.ExcludedLighterSets);
    }

    [Fact]
    public void ProgressionStartsFromTheHeaviestLoad_NotTheAverage()
    {
        var logs = new List<LoggedSet>
        {
            new(100m, 12, 1),
            new(100m, 12, 1),
            new(80m, 12, 1)
        };

        var load = WorkingLoad.Select(logs)!;
        var result = _engine.ComputeNext(
            load.ReferenceWeightKg,
            load.WorkingSets,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m);

        // Prosek bi bio 93.33 kg, a predlog 95 kg - ispod onoga što je podignuto.
        Assert.Equal(102.5m, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
    }

    [Fact]
    public void ProgressionCountsALighterSetThatMissedTheRangeAgainstTheReferenceLoad()
    {
        // Lakša serija ispod donjeg dela opsega, bez rezerve: efektivni RIR je -3, pa
        // korekcija zaista pomera težinu, ne samo zaokruženje.
        var load = WorkingLoad.Select([
            new LoggedSet(100m, 12, 1),
            new LoggedSet(100m, 12, 1),
            new LoggedSet(80m, 5, 0)
        ])!;

        var result = _engine.ComputeNext(
            load.ReferenceWeightKg,
            load.WorkingSets,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m);

        // Prosek efektivnog RIR-a (1 + 1 - 3) / 3 = -0.333, odstupanje -1.333 => -4%.
        Assert.Equal(95m, result.NextWeightKg);
        Assert.Equal(3, load.WorkingSets.Count);
    }

    [Fact]
    public void ProgressionCountsALighterFailureAgainstTheReferenceLoad()
    {
        var load = WorkingLoad.Select([
            new LoggedSet(100m, 12, 1),
            new LoggedSet(100m, 12, 0, IsFailure: true),
            new LoggedSet(90m, 10, 0, IsFailure: true)
        ])!;

        var result = _engine.ComputeNext(
            load.ReferenceWeightKg,
            load.WorkingSets,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m);

        // Serija sa 10 ponavljanja obara "sve na vrhu", pa koraka nema: prosek efektivnog
        // RIR-a je (1 + 0 + 0) / 3 = 0.333, odstupanje -0.667 => -2% => 98 -> 97.5 kg.
        Assert.Equal(97.5m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }
}
