using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Korekcija po RIR-u čita istu Epley krivu kao procena maksimuma, radna težina i upijanje
/// koraka (runda 15). Do tada je bila ravnih 3% po poenu za svaki propis.
/// </summary>
public class EpleyCorrectionTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    // Snaga 3-6 uz RIR 2: sredina 4.5, 1 / 36.5 = 2.74%.
    [InlineData(3, 6, 2, 36.5)]
    // Fiksnih 5 uz RIR 2 (5x5 iz ličnog šablona).
    [InlineData(5, 5, 2, 37)]
    // Hipertrofija 8-12 uz RIR 1: 1 / 41 = 2.44%.
    [InlineData(8, 12, 1, 41)]
    // Izolacija 10-20 uz RIR 1: 1 / 46 = 2.17%.
    [InlineData(10, 20, 1, 46)]
    // Izolacija u bloku snage, 10-20 uz RIR 2.
    [InlineData(10, 20, 2, 47)]
    public void TheRatePerPoint_IsEpleysAtTheMiddleOfTheRange(int min, int max, int targetRir, double denominator)
    {
        Assert.Equal(1m / (decimal)denominator, ProgressionEngine.CorrectionPerRirPoint(min, max, targetRir));
    }

    /// <summary>
    /// Na sredini opsega korekcija čuva procenu maksimuma tačno: posle nje isti broj
    /// ponavljanja pada na ciljni RIR. Korak od 0.01 kg je tu da zaokruživanje ne sakrije
    /// grešku od nekoliko desetina procenta.
    /// </summary>
    [Theory]
    [InlineData(3, 7, 2)]
    [InlineData(8, 12, 1)]
    [InlineData(10, 20, 1)]
    [InlineData(10, 20, 2)]
    public void AtTheMiddleOfTheRange_TheNextLoadKeepsTheEstimatedMax(int min, int max, int targetRir)
    {
        var middle = (min + max) / 2;

        for (var deviation = -1; deviation <= 3; deviation++)
        {
            if (deviation == 0)
            {
                continue;
            }

            var rir = targetRir + deviation;
            var sets = new[] { new WorkingSet(middle, rir), new WorkingSet(middle, rir), new WorkingSet(middle, rir) };

            var next = _engine.ComputeNext(100m, sets, targetRir, min, max, 0.01m).NextWeightKg;
            var epley = Math.Round(100m * (30m + middle + rir) / (30m + middle + targetRir), 2);

            Assert.Equal(epley, next);
        }
    }

    /// <summary>
    /// Ista rezerva iznad cilja vredi manje opterećenja u dužoj seriji, kao i po Epley-u.
    /// </summary>
    [Fact]
    public void TheSameDeviation_MovesALongSetLessThanAShortOne()
    {
        var shortSets = new[] { new WorkingSet(4, 4), new WorkingSet(4, 4), new WorkingSet(4, 4) };
        var longSets = new[] { new WorkingSet(15, 3), new WorkingSet(15, 3), new WorkingSet(15, 3) };

        var strength = _engine.ComputeNext(100m, shortSets, targetRir: 2, repRangeMin: 3, repRangeMax: 6, weightStepKg: 0.01m);
        var isolation = _engine.ComputeNext(100m, longSets, targetRir: 1, repRangeMin: 10, repRangeMax: 20, weightStepKg: 0.01m);

        // Oba su dva poena iznad cilja: 2 / 36.5 = 5.48% naspram 2 / 46 = 4.35%.
        Assert.Equal(105.48m, strength.NextWeightKg);
        Assert.Equal(104.35m, isolation.NextWeightKg);
    }

    /// <summary>
    /// Slučaj zbog koga je stopa promenjena: sajla od 50 kg na izolaciji 10-20, tri serije od
    /// 15 sa RIR 4 uz cilj 1. Ravnih 3% po poenu je davalo +9%, 54.5 kg, zaokruženo 55. Po
    /// Epley-u 15 ponavljanja uz RIR 1 ide na 50 * 49 / 46 = 53.26 kg - korak ispod toga.
    /// </summary>
    [Fact]
    public void AnEasyIsolationSession_NoLongerOvershootsTheLoadItsRepsJustify()
    {
        var sets = new[] { new WorkingSet(15, 4), new WorkingSet(15, 4), new WorkingSet(15, 4) };

        var result = _engine.ComputeNext(50m, sets, targetRir: 1, repRangeMin: 10, repRangeMax: 20, weightStepKg: 2.5m);

        Assert.Equal(52.5m, result.NextWeightKg);
    }

    /// <summary>
    /// Korak naniže kad zaokruživanje obriše korekciju zavisi od toga koliko je sesija bila
    /// teža od cilja, a ne od toga da li je korekcija dotakla -10%. Granica je tri poena.
    /// </summary>
    [Theory]
    // Šest do otkaza: dva ispod dna, odstupanje -3 => -7.3%, 9.27 kg se zaokruži na 10.
    [InlineData(6, 0, true, 8)]
    // Sedam do otkaza: odstupanje -2, vežbač gradi ponavljanja na istoj težini.
    [InlineData(7, 0, true, 10)]
    // Osam bez rezerve: na dnu opsega, odstupanje -1.
    [InlineData(8, 0, false, 10)]
    public void ALightLoadStepsDown_OnlyFromThreeRepsHarderThanTheTarget(int reps, int rir, bool isFailure, double expectedKg)
    {
        var sets = new[] { new WorkingSet(reps, rir, isFailure), new WorkingSet(reps, rir, isFailure), new WorkingSet(reps, rir, isFailure) };

        var result = _engine.ComputeNext(10m, sets, targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: 2m);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }
}
