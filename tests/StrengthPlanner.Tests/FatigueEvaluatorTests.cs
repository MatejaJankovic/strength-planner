using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Deload je do sada bio vezan za kalendar (četvrta nedelja). Ovi testovi pokrivaju
/// ocenu umora koja može da ga povuče ranije.
/// </summary>
public class FatigueEvaluatorTests
{
    private static WeeklyFatigue Fresh => new(
        AverageRirDeviation: 0m,
        FailureShare: 0m,
        E1RmChangeShare: 0m,
        VolumeVsMrvShare: 0m);

    [Fact]
    public void Score_IsZero_ForAWeekThatWentToPlan()
    {
        Assert.Equal(0m, FatigueEvaluator.Score(Fresh));
    }

    [Fact]
    public void Score_IsOne_WhenEverySignalIsMaxedOut()
    {
        var wrecked = new WeeklyFatigue(
            AverageRirDeviation: -3m,
            FailureShare: 1m,
            E1RmChangeShare: -0.20m,
            VolumeVsMrvShare: 1.5m);

        Assert.Equal(1m, FatigueEvaluator.Score(wrecked));
    }

    [Fact]
    public void Score_IgnoresSignalsPointingTheOtherWay()
    {
        // Nedelja lakša od plana, sa rastom snage i malim volumenom: pozitivni signali
        // ne smeju da daju negativnu ocenu niti da "kompenzuju" nešto drugo.
        var easy = new WeeklyFatigue(
            AverageRirDeviation: 2m,
            FailureShare: 0m,
            E1RmChangeShare: 0.08m,
            VolumeVsMrvShare: 0.3m);

        Assert.Equal(0m, FatigueEvaluator.Score(easy));
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
        var fatigue = new WeeklyFatigue(
            (decimal)rirDeviation,
            (decimal)failureShare,
            (decimal)e1RmChange,
            (decimal)volumeShare);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void ShouldDeload_IsTrue_WhenSeveralSignalsAgree()
    {
        // Serije osetno teže od plana, četvrtina do otkaza, procena snage pala 3%,
        // volumen na samom MRV-u — svaki od njih je sumnjiv, zajedno su jasni.
        var fatigue = new WeeklyFatigue(
            AverageRirDeviation: -1.5m,
            FailureShare: 0.25m,
            E1RmChangeShare: -0.03m,
            VolumeVsMrvShare: 1m);

        Assert.True(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void ShouldDeload_IsFalse_ForAHardButProductiveWeek()
    {
        // Naporna nedelja u kojoj snaga i dalje raste nije razlog za deload;
        // to je upravo nedelja zbog koje se trenira.
        var fatigue = new WeeklyFatigue(
            AverageRirDeviation: -1m,
            FailureShare: 0.2m,
            E1RmChangeShare: 0.02m,
            VolumeVsMrvShare: 0.9m);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void Score_TreatsMissingE1RmComparisonAsNeutral()
    {
        // Prva nedelja nema sa čim da se poredi; nedostatak podatka ne sme da se
        // protumači kao pad performansi.
        var withoutComparison = new WeeklyFatigue(-1m, 0.2m, 0m, 0.85m);
        var withDrop = withoutComparison with { E1RmChangeShare = -0.05m };

        Assert.True(FatigueEvaluator.Score(withDrop) > FatigueEvaluator.Score(withoutComparison));
    }

    [Fact]
    public void Score_DoesNotCountVolumeBelowEightyPercentOfMrv()
    {
        var wellBelow = new WeeklyFatigue(0m, 0m, 0m, 0.5m);
        var atFloor = new WeeklyFatigue(0m, 0m, 0m, 0.8m);

        Assert.Equal(0m, FatigueEvaluator.Score(wellBelow));
        Assert.Equal(0m, FatigueEvaluator.Score(atFloor));
    }

    [Fact]
    public void Score_NeverLeavesTheZeroToOneRange()
    {
        var extremes = new[]
        {
            new WeeklyFatigue(-100m, 5m, -5m, 10m),
            new WeeklyFatigue(100m, -5m, 5m, -10m),
            new WeeklyFatigue(0m, 0m, 0m, 0m)
        };

        foreach (var fatigue in extremes)
        {
            var score = FatigueEvaluator.Score(fatigue);

            Assert.InRange(score, 0m, 1m);
        }
    }
}
