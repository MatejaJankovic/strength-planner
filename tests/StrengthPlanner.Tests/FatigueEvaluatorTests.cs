using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Deload je do sada bio vezan za kalendar (četvrta nedelja). Ovi testovi pokrivaju
/// ocenu umora koja može da ga povuče ranije.
/// </summary>
public class FatigueEvaluatorTests
{
    /// <summary>Nedelja hipertrofije (ciljni RIR 1) koja je prošla tačno po planu.</summary>
    private static WeeklyFatigue Hypertrophy(
        decimal rirDeviation = 0m,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m) =>
        new(rirDeviation, AchievableRirDeficit: 1m, failureShare, e1RmChange, volumeShare);

    /// <summary>Nedelja snage (ciljni RIR 2).</summary>
    private static WeeklyFatigue Strength(
        decimal rirDeviation = 0m,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m) =>
        new(rirDeviation, AchievableRirDeficit: 2m, failureShare, e1RmChange, volumeShare);

    [Fact]
    public void Score_IsZero_ForAWeekThatWentToPlan()
    {
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy()));
    }

    [Fact]
    public void Score_IsOne_WhenEverySignalIsMaxedOut()
    {
        var wrecked = Hypertrophy(
            rirDeviation: -3m,
            failureShare: 1m,
            e1RmChange: -0.20m,
            volumeShare: 1.5m);

        Assert.Equal(1m, FatigueEvaluator.Score(wrecked));
    }

    [Fact]
    public void Score_IgnoresSignalsPointingTheOtherWay()
    {
        // Nedelja lakša od plana, sa rastom snage i malim volumenom: pozitivni signali
        // ne smeju da daju negativnu ocenu niti da "kompenzuju" nešto drugo.
        var easy = Hypertrophy(rirDeviation: 2m, e1RmChange: 0.08m, volumeShare: 0.3m);

        Assert.Equal(0m, FatigueEvaluator.Score(easy));
    }

    [Fact]
    public void Score_TreatsGoalsWithDifferentTargetRirTheSame()
    {
        // Vežbač koji je celu nedelju grebao dno svoje skale (RIR 0 uz cilj 1, odnosno
        // uz cilj 2) mora dobiti isti signal — inače hipertrofija, kao podrazumevani
        // cilj, nikada ne bi mogla da iskoristi ovaj udeo.
        var hypertrophyAtZeroRir = Hypertrophy(rirDeviation: -1m);
        var strengthAtZeroRir = Strength(rirDeviation: -2m);

        Assert.Equal(
            FatigueEvaluator.Score(strengthAtZeroRir),
            FatigueEvaluator.Score(hypertrophyAtZeroRir));
    }

    [Fact]
    public void ShouldDeload_TriggersForHypertrophy_WhenTheWeekGrindsAndStrengthDrops()
    {
        // Regresija: dok se odstupanje merilo fiksnom skalom od dva poena, hipertrofija
        // (ciljni RIR 1) nije mogla da pređe prag ni sa svim ostalim signalima na
        // maksimumu — najviše 0.575 od potrebnih 0.60.
        var grinding = Hypertrophy(rirDeviation: -1m, e1RmChange: -0.05m, volumeShare: 1m);

        Assert.True(FatigueEvaluator.ShouldDeload(grinding));
    }

    [Theory]
    // Nijedan pojedinačni signal ne sme sam da pređe prag: najteži nosi 0.35, prag je 0.60.
    [InlineData(-3, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, -0.20, 0)]
    [InlineData(0, 0, 0, 1.5)]
    public void ShouldDeload_IsFalse_WhenOnlyOneSignalIsMaxedOut(
        double rirDeviation,
        double failureShare,
        double e1RmChange,
        double volumeShare)
    {
        var fatigue = Hypertrophy(
            (decimal)rirDeviation,
            (decimal)failureShare,
            (decimal)e1RmChange,
            (decimal)volumeShare);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void Score_KeepsTheRirAndFailureSignalsIndependent()
    {
        // Otkazi ulaze isključivo kroz FailureShare. Da su ulazili i u prosek RIR-a,
        // jedan te isti događaj bi popunio oba najteža signala i "moraju se složiti bar
        // dva" ne bi značilo ništa.
        var completedSetsWentToPlan = Hypertrophy(rirDeviation: 0m, failureShare: 0.5m);

        // 0.25 od maksimalnog udela za otkaze, ništa od RIR-a.
        Assert.Equal(0.25m, FatigueEvaluator.Score(completedSetsWentToPlan));
        Assert.False(FatigueEvaluator.ShouldDeload(completedSetsWentToPlan));
    }

    /// <summary>
    /// Nedelja u kojoj nijedna serija nije dovršena nosi tu činjenicu kroz udeo otkaza, i
    /// samo kroz njega. Ranije je ista činjenica dizala i RIR signal na najgore očitavanje
    /// (1.0), pa je 0.35 + 0.25 davalo tačno prag — jedan uzrok je pokretao deload, u
    /// pravilu koje kaže da nijedan signal to ne može sam.
    /// </summary>
    [Fact]
    public void Score_ReadsAWeekWithoutACompletedSet_ThroughTheFailureShareAlone()
    {
        var everySetFailed = Hypertrophy(rirDeviation: 0m, failureShare: 1m);

        Assert.Equal(0.25m, FatigueEvaluator.Score(everySetFailed));
        Assert.False(FatigueEvaluator.ShouldDeload(everySetFailed));
    }

    /// <summary>
    /// Ista nedelja sa još jednim stvarnim signalom i dalje pokreće deload. To je razlika
    /// između "sve je išlo do otkaza" i "sve je išlo do otkaza, a snaga je pala".
    /// </summary>
    [Fact]
    public void ADeload_StillFollows_WhenASecondSignalAgrees()
    {
        Assert.Equal(0.50m, FatigueEvaluator.Score(Hypertrophy(failureShare: 1m, e1RmChange: -0.05m)));
        Assert.Equal(0.40m, FatigueEvaluator.Score(Hypertrophy(failureShare: 1m, volumeShare: 1m)));
        Assert.True(FatigueEvaluator.ShouldDeload(
            Hypertrophy(failureShare: 1m, e1RmChange: -0.05m, volumeShare: 1m)));
    }

    /// <summary>
    /// Merenje koje je porušilo staro pravilo: jedna serija od dvadeset je pomerala ocenu
    /// za 0.35 i odlučivala o deload-u. Dvadeset otkaza je davalo 0.60, a devetnaest otkaza
    /// uz jednu dovršenu seriju na cilju 0.25 — litica, a ne mera.
    /// </summary>
    [Fact]
    public void OneCompletedSet_NoLongerSwingsTheScoreByATerm()
    {
        var everySetFailed = Hypertrophy(rirDeviation: 0m, failureShare: 1m);
        var oneSetCompleted = Hypertrophy(rirDeviation: 0m, failureShare: 19m / 20m);

        var gap = FatigueEvaluator.Score(everySetFailed) - FatigueEvaluator.Score(oneSetCompleted);

        // Tacno nula, a ne samo malo: udeo otkaza dostize punu tezinu na 0.5, pa i 0.95 i
        // 1.0 nose isti maksimum. Ranije je ta jedna serija menjala ocenu za 0.35 i sama
        // odlucivala o deload-u.
        Assert.Equal(0m, gap);
    }

    [Fact]
    public void ShouldDeload_IsFalse_ForAHardButProductiveWeek()
    {
        // Naporna nedelja u kojoj snaga i dalje raste nije razlog za deload;
        // to je upravo nedelja zbog koje se trenira.
        var fatigue = Hypertrophy(
            rirDeviation: -1m,
            failureShare: 0.2m,
            e1RmChange: 0.02m,
            volumeShare: 0.85m);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void Score_TreatsMissingE1RmComparisonAsNeutral()
    {
        // Prva nedelja nema sa čim da se poredi; nedostatak podatka ne sme da se
        // protumači kao pad performansi.
        var withoutComparison = Hypertrophy(rirDeviation: -0.5m, failureShare: 0.2m, volumeShare: 0.85m);
        var withDrop = withoutComparison with { E1RmChangeShare = -0.05m };

        Assert.True(FatigueEvaluator.Score(withDrop) > FatigueEvaluator.Score(withoutComparison));
    }

    [Fact]
    public void Score_DoesNotCountVolumeBelowEightyPercentOfMrv()
    {
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy(volumeShare: 0.5m)));
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy(volumeShare: 0.8m)));
    }

    [Fact]
    public void Score_NeverLeavesTheZeroToOneRange()
    {
        var extremes = new[]
        {
            new WeeklyFatigue(-100m, 1m, 5m, -5m, 10m),
            new WeeklyFatigue(100m, 1m, -5m, 5m, -10m),
            new WeeklyFatigue(0m, 0m, 0m, 0m, 0m),
            new WeeklyFatigue(-1m, -3m, 0m, 0m, 0m)
        };

        foreach (var fatigue in extremes)
        {
            var score = FatigueEvaluator.Score(fatigue);

            Assert.InRange(score, 0m, 1m);
        }
    }

    /// <summary>
    /// Ista funkcija sada služi i granicama volumena, pa prima ponder po seriji (doprinos
    /// mišiću × blizina otkaza). Važno je da ponder ne vraća otkaze u prosek: granice su
    /// računale SVOJU verziju nad svim serijama, i na ove tri je dobijala −2 tamo gde ocena
    /// umora dobija 0 — pa je jedan otkaz zatvarao oba uslova I-testa "imao je rezerve".
    /// </summary>
    [Fact]
    public void AverageRirDeviation_TakesAWeight_AndStillLeavesFailuresOut()
    {
        RirSample[] sets =
        [
            new(new WorkingSet(10, 1), 8, 1, Weight: 1m),
            new(new WorkingSet(10, 1), 8, 1, Weight: 0.5m),
            new(new WorkingSet(3, 0, IsFailure: true), 8, 1, Weight: 1m)
        ];

        Assert.Equal(0m, FatigueEvaluator.AverageRirDeviation(sets));

        // Stara računica granica volumena, ostavljena kao oracle: prosek nad SVIM serijama.
        var overEverySet = sets.Average(sample =>
            (decimal)(sample.Set.EffectiveRir(sample.RepRangeMin) - sample.TargetRir));

        Assert.Equal(-2m, overEverySet);
    }

    /// <summary>Ponder zaista pomera prosek, i to u odnosu na svoju sumu, ne na broj serija.</summary>
    [Fact]
    public void AverageRirDeviation_LeansTowardTheHeavierSet()
    {
        RirSample[] sets =
        [
            new(new WorkingSet(10, 3), 8, 1, Weight: 3m),
            new(new WorkingSet(10, 1), 8, 1, Weight: 1m)
        ];

        // (3 × 2 + 1 × 0) / 4
        Assert.Equal(1.5m, FatigueEvaluator.AverageRirDeviation(sets));
    }

    [Fact]
    public void AverageRirDeviation_ReadsABelowFloorSetWithReserveAsHarder()
    {
        // 5 ponavljanja sa RIR 2 u 8-12 @RIR1: kapacitet 7, jedno ispod dna. Progresiji je
        // to -2 poena; sirov RIR bi ovde rekao +1, "lakše od plana".
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 2), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(-2m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_KeepsLoggedRirInsideTheRange()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(10, 0), RepRangeMin: 8, TargetRir: 1),
            new RirSample(new WorkingSet(9, 3), RepRangeMin: 8, TargetRir: 1)
        ]);

        // (-1 + 2) / 2
        Assert.Equal(0.5m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_LeavesFailuresToTheFailureShare()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 0, IsFailure: true), RepRangeMin: 8, TargetRir: 1),
            new RirSample(new WorkingSet(10, 2), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(1m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_CountsAnUnflaggedImpliedFailureAmongTheCompletedSets()
    {
        // Domenska funkcija ne zna za zastavicu iz baze: dobija ono što joj pozivalac
        // preda. SetLogService danas upisuje IsFailure = true i kad kvačica nije dotaknuta
        // (ImpliesFailure), ali serije upisane pre te izmene i dalje mogu da stoje sa
        // false. Takva serija ne ulazi u udeo otkaza, koji čita zastavicu, pa mora da
        // ostane u proseku RIR-a - inače nedelja u kojoj je vežbač promašio opseg ne bi
        // imala nijedan signal umora.
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(6, 0), RepRangeMin: 8, TargetRir: 1)
        ]);

        // Kapacitet 6 naspram dna 8 => -2, minus cilj 1.
        Assert.Equal(-3m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_IsZero_WhenEverySetFailed()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 0, IsFailure: true), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(0m, deviation);
    }
}
