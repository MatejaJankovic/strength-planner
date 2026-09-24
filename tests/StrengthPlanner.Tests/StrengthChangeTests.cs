using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Ocena umora čita pad procenjene snage kao jedan od četiri signala, i merila ga je kao
/// najbolju procenu nedelje naspram najbolje procene prethodne — po vežbi, bez pitanja da
/// li su te dve serije uopšte uporedive.
///
/// Gde u propisanom opsegu vežbač padne je i samo deo propisa, i pomera se. Serija na vrhu
/// jedne nedelje i serija na dnu sledeće su **obe po propisu**, a razlika između njih po
/// Epley-u iznosi skoro deset odsto — više nego dvostruko od 5% na kojima taj signal
/// dostiže punu težinu.
/// </summary>
public class StrengthChangeTests
{
    private static readonly Guid Bench = new("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Squat = new("00000000-0000-0000-0000-0000000000b2");

    /// <summary>
    /// Merenje koje je pokrenulo ovu granu, sada kao test. Nedelje 3 i 4 linearnog
    /// hipertrofijskog bloka: u prvoj vežbač pogodi vrh opsega, u drugoj dno — oba puta
    /// tačno ono što propis traži, uz opterećenje izvedeno iz istog maksimuma.
    /// </summary>
    [Fact]
    public void LandingAtTheTopAndThenAtTheFloor_IsNoLongerReadAsACollapse()
    {
        var calculator = new E1RmCalculator();
        var goal = GoalPrescriptions.ForGoal(Goal.Hypertrophy);
        var weeks = Periodization
            .ForBlock(PeriodizationModel.Linear, goal.RepRangeMin, goal.RepRangeMax, goal.TargetRir, 4)
            .Where(week => !week.IsDeload)
            .ToList();

        var before = weeks[2];
        var after = weeks[3];
        var loadBefore = calculator.WorkingWeightFor(140m, before.RepRangeMin, before.TargetRir, 2.5m);
        var loadAfter = calculator.WorkingWeightFor(140m, after.RepRangeMin, after.TargetRir, 2.5m);

        // Stara mera: najbolje naspram najboljeg, bez uslova uporedivosti.
        var topBefore = calculator.EstimateOneRepMax(loadBefore, before.RepRangeMax, before.TargetRir);
        var floorAfter = calculator.EstimateOneRepMax(loadAfter, after.RepRangeMin, after.TargetRir);
        var oldReading = (floorAfter - topBefore) / topBefore;

        Assert.True(oldReading < -0.09m, $"Staro čitanje je {oldReading:P1}, očekivano ispod -9%.");

        // Nova mera: nema uporedive serije (12 + 1 naspram 7 + 1), pa nema ni dokaza.
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, after.RepRangeMin, after.TargetRir, loadAfter)],
            [new StrengthSample(Bench, before.RepRangeMax, before.TargetRir, loadBefore)]);

        Assert.Null(change);
    }

    /// <summary>
    /// Isti broj ponavljanja, manje opterećenje: to je pad snage i mora da se vidi ceo.
    /// </summary>
    [Fact]
    public void SameRepsWithLessLoad_ReadsAsTheRealDecline()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 10, 1, 90m)],
            [new StrengthSample(Bench, 10, 1, 100m)]);

        Assert.NotNull(change);
        Assert.Equal(-0.10m, change!.Value, precision: 10);
    }

    /// <summary>Isti broj ponavljanja, veće opterećenje: napredak, istom merom.</summary>
    [Fact]
    public void SameRepsWithMoreLoad_ReadsAsProgress()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 10, 1, 105m)],
            [new StrengthSample(Bench, 10, 1, 100m)]);

        Assert.Equal(0.05m, change!.Value, precision: 10);
    }

    /// <summary>
    /// Tolerancija od jednog ponavljanja: 11 naspram 12 se poredi, a razliku plaća Epley,
    /// ne ćutanje. Bez nje bi vežbač koji jedne nedelje uradi 11 a druge 12 ostao bez
    /// signala.
    /// </summary>
    [Fact]
    public void OneRepOfTolerance_KeepsTheSignalAlive()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 11, 1, 100m)],
            [new StrengthSample(Bench, 10, 1, 100m)]);

        Assert.NotNull(change);

        // 100 × (1 + 12/30) naspram 100 × (1 + 11/30): +2.4%, a ne nula i ne pad.
        Assert.True(change!.Value > 0m, $"Promena je {change.Value}.");
        Assert.True(change.Value < 0.03m, $"Promena je {change.Value}.");
    }

    [Fact]
    public void TwoRepsApart_IsNotComparable()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 12, 1, 100m)],
            [new StrengthSample(Bench, 9, 1, 100m)]);

        Assert.Null(change);
    }

    /// <summary>
    /// Vežba koje u prethodnoj nedelji nije bilo ne ulazi u prosek, a vežba koja ima par
    /// ulazi — poređenje je po vežbi, pa jedan nov pokret ne pravi lažan pad.
    /// </summary>
    [Fact]
    public void OnlyExercisesWithAPair_EnterTheAverage()
    {
        var change = StrengthChange.ChangeShare(
            [
                new StrengthSample(Bench, 10, 1, 90m),
                new StrengthSample(Squat, 10, 1, 200m)
            ],
            [new StrengthSample(Bench, 10, 1, 100m)]);

        Assert.Equal(-0.10m, change!.Value, precision: 10);
    }

    /// <summary>
    /// Serija koja nigde u sistemu ne daje procenu ne daje je ni ovde: isti predikat
    /// (<see cref="E1RmCalculator.CanEstimateFrom"/>) odlučuje i o trendu, i o rekordima,
    /// i o ovom signalu.
    /// </summary>
    [Theory]
    [InlineData(13, 0)]
    [InlineData(10, 4)]
    public void ASetTheSystemWouldNotEstimateFrom_IsNoEvidenceHereEither(int reps, int rir)
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, reps, rir, 90m)],
            [new StrengthSample(Bench, reps, rir, 100m)]);

        Assert.Null(change);
    }

    [Fact]
    public void AWeekWithNothingToCompareAgainst_ReportsNoChange()
    {
        Assert.Null(StrengthChange.ChangeShare([new StrengthSample(Bench, 10, 1, 100m)], []));
        Assert.Null(StrengthChange.ChangeShare([], [new StrengthSample(Bench, 10, 1, 100m)]));
        Assert.Null(StrengthChange.ChangeShare([], []));
    }

    /// <summary>
    /// Kada nedelja ima i lakše i teže serije na uporedivom broju ponavljanja, pad se meri
    /// prema najboljoj od njih — zagrevanje ne sme da izgleda kao slabost.
    /// </summary>
    [Fact]
    public void TheBestComparableSetOfTheWeek_IsWhatCounts()
    {
        var change = StrengthChange.ChangeShare(
            [
                new StrengthSample(Bench, 10, 1, 60m),
                new StrengthSample(Bench, 10, 1, 100m)
            ],
            [new StrengthSample(Bench, 10, 1, 100m)]);

        Assert.Equal(0m, change!.Value);
    }

    /// <summary>
    /// A ako je vežbač u prethodnoj nedelji imao i bolju i lošiju uporedivu seriju, meri se
    /// prema boljoj: to je ono što je tada mogao.
    /// </summary>
    [Fact]
    public void TheBestComparableSetOfTheEarlierWeek_IsTheReference()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 10, 1, 100m)],
            [
                new StrengthSample(Bench, 10, 1, 100m),
                new StrengthSample(Bench, 10, 1, 110m)
            ]);

        Assert.True(change!.Value < 0m, $"Promena je {change.Value}.");
        Assert.Equal(-10m / 110m, change.Value, precision: 10);
    }
}
