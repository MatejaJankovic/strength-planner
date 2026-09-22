using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Sesija u kojoj je svaka serija stigla do vrha opsega.
///
/// Ranije je negativna korekcija po RIR-u išla preko koraka duple progresije: 0.97u + korak.
/// To je držalo opterećenje samo između ~42 i 125 kg (za šipku), iznad je obaralo, a
/// sažetak je svejedno pokazivao strelicu naviše. Sada povratak na dno opsega plaća manjak
/// RIR-a do širine opsega, pa korak ide ceo; samo uska nedelja izvučena preko cilja drži.
/// </summary>
public class TopOfRangeProgressionTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    // Hipertrofija 8-12 @RIR1, 3x12 do otkaza: korak na svakoj težini i svakom koraku.
    [InlineData(100.0, 2.5, 8, 12, 1, 12, 0, true, 102.5)]
    [InlineData(160.0, 2.5, 8, 12, 1, 12, 0, true, 162.5)]
    [InlineData(30.0, 2.0, 8, 12, 1, 12, 0, true, 32.0)]
    [InlineData(110.0, 2.0, 8, 12, 1, 12, 0, true, 112.0)]
    [InlineData(200.0, 5.0, 8, 12, 1, 12, 0, true, 205.0)]
    [InlineData(20.0, 0.5, 8, 12, 1, 12, 0, true, 20.5)]
    // Snaga 3-6 @RIR2, 3x6 @RIR1 - prijavljeni slučaj: 140 je davalo 137.5, 180 je davalo 177.5.
    [InlineData(140.0, 2.5, 3, 6, 2, 6, 1, false, 142.5)]
    [InlineData(180.0, 2.5, 3, 6, 2, 6, 1, false, 182.5)]
    // Granica: manjak (1 poen) tačno jednak širini uskog opsega (1) - korak još ide.
    [InlineData(100.0, 2.5, 11, 12, 2, 12, 1, false, 102.5)]
    // Težina van mreže koraka (101) ne sme da se zaokruži ispod podignute.
    [InlineData(101.0, 2.5, 8, 12, 1, 12, 0, true, 102.5)]
    public void ComputeNext_AddsOneStep_WhenRangeResetCoversRirShortfall(
        double usedKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        int rir,
        bool isFailure,
        double expectedKg)
    {
        var result = ComputeForThreeSets(usedKg, stepKg, repRangeMin, repRangeMax, targetRir, reps, rir, isFailure);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
    }

    [Theory]
    // Linearna hipertrofija, nedelja 1 (11-12 @RIR2), 3x12 sa RIR 0: po Epley-u je idealno
    // 97.7 kg, ali vrh opsega ne spušta opterećenje - drži se 100.
    [InlineData(100.0, 2.5, 11, 12, 2, 12, 0, false, 100.0)]
    // Uska nedelja snage 3-4 @RIR3, 3x4 sa RIR 1.
    [InlineData(140.0, 2.5, 3, 4, 3, 4, 1, false, 140.0)]
    // Držanje vraća tačno podignutu težinu, bez zaokruživanja 101 -> 100.
    [InlineData(101.0, 2.5, 11, 12, 2, 12, 0, true, 101.0)]
    public void ComputeNext_HoldsLoad_WhenRirShortfallExceedsRangeReset(
        double usedKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        int rir,
        bool isFailure,
        double expectedKg)
    {
        var result = ComputeForThreeSets(usedKg, stepKg, repRangeMin, repRangeMax, targetRir, reps, rir, isFailure);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_KeepsPositiveCorrectionOnTopOfStep()
    {
        // Lakše od plana na vrhu opsega: teza traži i korekciju naviše i korak.
        // 160 * 1.06 = 169.6 + 2.5 = 172.1 -> 68.84 koraka -> 69 -> 172.5. Čuva pravilo od
        // "popravke" koja bi korekciju na vrhu jednostavno izbacila.
        var result = ComputeForThreeSets(160.0, 2.5, 8, 12, 1, 12, 3, false);

        Assert.Equal(172.5m, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
    }

    private ProgressionResult ComputeForThreeSets(
        double usedKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        int rir,
        bool isFailure)
    {
        var sets = new List<WorkingSet>
        {
            new(reps, rir, isFailure),
            new(reps, rir, isFailure),
            new(reps, rir, isFailure)
        };

        return _engine.ComputeNext(
            (decimal)usedKg,
            sets,
            targetRir,
            repRangeMin,
            repRangeMax,
            (decimal)stepKg);
    }
}
