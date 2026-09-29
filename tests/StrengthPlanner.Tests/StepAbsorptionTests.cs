using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Korak koji opseg ne može da upije, korekcija koju korak ne može da izrazi, i deload koji
/// se zaokruživao nazad na punu težinu. Sva tri su isti koren: korak tega veliki u odnosu na
/// težinu, što je kod bučica i malih mašina uobičajeno, a nijedan test nije proveravao
/// težine ispod 20 kg.
/// </summary>
public class StepAbsorptionTests
{
    private static readonly decimal[] Steps = [0.5m, 1m, 2m, 2.5m, 5m, 10m];

    private static readonly (int Min, int Max)[] Ranges = [(3, 6), (8, 12), (11, 12), (3, 4), (5, 5), (6, 9)];

    private readonly ProgressionEngine _engine = new();

    [Theory]
    // Šipka na stvarnim težinama: korak uvek staje, cilj ostaje vrh opsega.
    [InlineData(100, 2.5, 8, 12, 1, 12)]
    [InlineData(60, 2.5, 3, 6, 2, 6)]
    [InlineData(100, 2.5, 11, 12, 2, 12)]
    [InlineData(100, 2.5, 5, 5, 2, 5)]
    // Bučica 20 -> 22 kg je 10% i staje; 8 -> 10 kg je 25% i ne staje.
    [InlineData(20, 2, 8, 12, 1, 12)]
    [InlineData(8, 2, 8, 12, 1, 17)]
    // Mašina 20 -> 25 kg (25%), sajla 12.5 -> 15 kg (20%).
    [InlineData(20, 5, 8, 12, 1, 17)]
    [InlineData(12.5, 2.5, 8, 12, 1, 15)]
    public void RepsToEarnStep_IsTheTopOfTheRange_UnlessTheStepDoesNotFit(
        double loadKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int expected)
    {
        var reps = StepAbsorption.RepsToEarnStep((decimal)loadKg, (decimal)stepKg, repRangeMin, repRangeMax, targetRir);

        Assert.Equal(expected, reps);
    }

    [Fact]
    public void RepsToEarnStep_IsTheSmallestRepCountThatKeepsTheFloorReachable()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var load in Enumerable.Range(1, 120).Select(k => k * step))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        var reps = StepAbsorption.RepsToEarnStep(load, step, min, max, targetRir);

                        if (reps < max)
                        {
                            failures.Add($"{load}/{step} {min}-{max}@{targetRir}: {reps} below the top");
                        }

                        if (!FloorReachable(reps, load, step, min, targetRir))
                        {
                            failures.Add($"{load}/{step} {min}-{max}@{targetRir}: {reps} leaves the floor beyond failure");
                        }

                        if (reps > max && FloorReachable(reps - 1, load, step, min, targetRir))
                        {
                            failures.Add($"{load}/{step} {min}-{max}@{targetRir}: {reps} is not the smallest");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [Fact]
    public void ALightDumbbell_WaitsForTheRepsThatPayForTheStep_InsteadOfJumping25Percent()
    {
        // Izmereno na nepromenjenom kodu: 8 kg, 3 x 12 @RIR1 -> 10 kg, gde Epley ostavlja
        // oko 3.4 ponavljanja uz RIR 1 u opsegu koji počinje od 8.
        var atTheTop = _engine.ComputeNext(8m, Sets(12, 1), targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 2m);
        var belowTheTarget = _engine.ComputeNext(8m, Sets(16, 1), targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 2m);
        var atTheTarget = _engine.ComputeNext(8m, Sets(17, 1), targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 2m);

        Assert.Equal(8m, atTheTop.NextWeightKg);
        Assert.False(atTheTop.WeightIncreased);
        Assert.Equal(8m, belowTheTarget.NextWeightKg);
        Assert.Equal(10m, atTheTarget.NextWeightKg);
        Assert.True(atTheTarget.WeightIncreased);
    }

    [Fact]
    public void AMachineStepThatDoesNotFit_HoldsTheLoad()
    {
        var result = _engine.ComputeNext(20m, Sets(12, 1), targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 5m);

        Assert.Equal(20m, result.NextWeightKg);
    }

    [Fact]
    public void BetweenTheTopAndTheExtendedTarget_TheLoadIsNeverLowered()
    {
        // Zgib 12 @RIR0 u 11-12 @RIR1, 40 kg na pojasu uz 51.2 kg tela, korak 5 kg: korak od
        // 5.5% ne staje u opseg od jednog ponavljanja, a prva verzija ove izmene je sesiju
        // tada sudila kao sesiju usred opsega - korekcija od -3% je davala 35 kg.
        var result = _engine.ComputeNext(
            40m,
            Sets(12, 0),
            targetRir: 1,
            repRangeMin: 11,
            repRangeMax: 12,
            weightStepKg: 5m,
            bodyweightLoadKg: 51.2m);

        Assert.Equal(40m, result.NextWeightKg);
    }

    [Theory]
    // Dumbbell 10 kg, 5/4/4 @RIR1 u 8-12: korekcija -10% daje 9, što se zaokruživalo na 10.
    [InlineData(10, 2, 8)]
    [InlineData(8, 2, 6)]
    // Šipka i sajla na 12.5, mašina na 25: ista granica, 10% je tačno pola koraka.
    [InlineData(12.5, 2.5, 10)]
    [InlineData(25, 5, 20)]
    // Iznad granice zaokruživanje korekciju već izražava: nepromenjeno ponašanje.
    [InlineData(30, 5, 25)]
    [InlineData(100, 2.5, 90)]
    public void ACappedCorrectionThatRoundingErased_MovesTheLoadDownOneStep(
        double loadKg,
        double stepKg,
        double expectedKg)
    {
        var sets = new List<WorkingSet> { new(5, 1), new(4, 1), new(4, 1) };

        var result = _engine.ComputeNext((decimal)loadKg, sets, targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: (decimal)stepKg);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    [Fact]
    public void AnUncappedCorrectionThatRoundingErased_StillHoldsTheLoad()
    {
        // 7 @RIR0 ispod dna je otkaz jedno ponavljanje ispod opsega: odstupanje -2, -6%,
        // 9.4 kg se zaokružuje na 10. Korak naniže se daje samo na granici korekcije;
        // ovde vežbač gradi ponavljanja na istoj težini.
        var result = _engine.ComputeNext(10m, Sets(7, 0), targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 2m);

        Assert.Equal(10m, result.NextWeightKg);
    }

    [Theory]
    [InlineData(10, 0, 2, 8)]
    [InlineData(8, 0, 2, 6)]
    [InlineData(12.5, 0, 2.5, 10)]
    [InlineData(25, 0, 5, 20)]
    // Bez izmene gde 90% i dalje pada ispod pune težine.
    [InlineData(100, 0, 2.5, 90)]
    [InlineData(40, 0, 5, 35)]
    // Telo nosi ceo teret, pa je deload nula dodatih - što je i ranije bio.
    [InlineData(80, 80, 1, 0)]
    public void DeloadLoad_IsAlwaysLighterThanTheLoadItLightens(
        double fullTotalKg,
        double bodyweightKg,
        double stepKg,
        double expectedKg)
    {
        var deload = NextWeekLoad.DeloadLoad((decimal)fullTotalKg, (decimal)bodyweightKg, (decimal)stepKg);

        Assert.Equal((decimal)expectedKg, deload);
    }

    [Fact]
    public void DeloadLoad_IsStrictlyLighter_OnEveryLoadAndStep()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var load in Enumerable.Range(1, 160).Select(k => k * step).Concat([9m, 11m, 41.5m, 83.1m]))
            {
                var deload = NextWeekLoad.DeloadLoad(load, 0m, step);

                if (deload >= load)
                {
                    failures.Add($"{load}/{step}: deload {deload}");
                }

                if (deload % step != 0)
                {
                    failures.Add($"{load}/{step}: deload {deload} is off the step grid");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [Fact]
    public void BothDeloadPaths_UseTheSameRule()
    {
        // Nedelja posle završenog treninga i auto-deload su nekad nosili svaki svoju kopiju
        // 90%; sada obe idu kroz DeloadLoad.
        var nextWeek = NextWeekLoad.For(
            referenceWeightKg: 10m,
            progressionWeightKg: null,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 3),
            nextIsDeload: true,
            oneRepMaxKg: null,
            weightStepKg: 2m);

        Assert.Equal(NextWeekLoad.DeloadLoad(10m, 0m, 2m), nextWeek);
        Assert.Equal(8m, nextWeek);
    }

    [Theory]
    [InlineData(10, 2, 8)]
    [InlineData(9, 2, 8)]
    [InlineData(1, 2, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(100, 2.5, 97.5)]
    public void StepBelow_IsTheGridPointStrictlyBelow(double value, double step, double expected)
    {
        Assert.Equal((decimal)expected, WeightMath.StepBelow((decimal)value, (decimal)step));
    }

    private static List<WorkingSet> Sets(int reps, int rir)
    {
        return [new WorkingSet(reps, rir), new WorkingSet(reps, rir), new WorkingSet(reps, rir)];
    }

    private static bool FloorReachable(int reps, decimal load, decimal step, int min, int targetRir)
    {
        return (30m + reps + targetRir) * load >= (load + step) * (30m + min);
    }
}
