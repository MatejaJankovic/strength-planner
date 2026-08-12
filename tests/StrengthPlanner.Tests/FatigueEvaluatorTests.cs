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
        bool allSetsFailed = false,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m) =>
        new(rirDeviation, AchievableRirDeficit: 1m, allSetsFailed, failureShare, e1RmChange, volumeShare);

    /// <summary>Nedelja snage (ciljni RIR 2).</summary>
    private static WeeklyFatigue Strength(
        decimal rirDeviation = 0m,
        bool allSetsFailed = false,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m) =>
        new(rirDeviation, AchievableRirDeficit: 2m, allSetsFailed, failureShare, e1RmChange, volumeShare);

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
            allSetsFailed: true,
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
            allSetsFailed: false,
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

    [Fact]
    public void Score_TreatsAWeekWithoutASingleCompletedSetAsTheWorstReading()
    {
        // Kada nijedna serija nije dovršena, proseka nema — ali to odsustvo je najgore
        // moguće očitavanje, a ne neutralno.
        var everySetFailed = Hypertrophy(rirDeviation: 0m, allSetsFailed: true, failureShare: 1m);

        Assert.Equal(0.60m, FatigueEvaluator.Score(everySetFailed));
        Assert.True(FatigueEvaluator.ShouldDeload(everySetFailed));
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
            new WeeklyFatigue(-100m, 1m, true, 5m, -5m, 10m),
            new WeeklyFatigue(100m, 1m, false, -5m, 5m, -10m),
            new WeeklyFatigue(0m, 0m, false, 0m, 0m, 0m),
            new WeeklyFatigue(-1m, -3m, false, 0m, 0m, 0m)
        };

        foreach (var fatigue in extremes)
        {
            var score = FatigueEvaluator.Score(fatigue);

            Assert.InRange(score, 0m, 1m);
        }
    }
}
