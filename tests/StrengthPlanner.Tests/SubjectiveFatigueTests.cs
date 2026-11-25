using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Odgovor vežbača o umoru (1-5) i kako se iz nedelja odgovora dobija porast nad njegovom sopstvenom osnovom.
/// Nivo odgovora se ne čita, jer ljudi različito koriste skalu: čita se porast.
/// </summary>
public class SubjectiveFatigueTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    public void OnlyOneToFive_IsAnAnswer(int rating, bool expected)
    {
        Assert.Equal(expected, SubjectiveFatigue.IsValid(rating));
    }

    [Fact]
    public void TheMeanOfAWeek_IsNull_WhenThereWereNoAnswers()
    {
        Assert.Null(SubjectiveFatigue.WeekMean([]));
        Assert.Equal(3m, SubjectiveFatigue.WeekMean([2, 3, 4]));
        Assert.Equal(2.5m, SubjectiveFatigue.WeekMean([2, 3]));
    }

    [Fact]
    public void ARise_NeedsBothTheWeekAndSomethingToCompareWith()
    {
        Assert.Equal(0m, SubjectiveFatigue.Rise(null, [2m]));
        Assert.Equal(0m, SubjectiveFatigue.Rise(3m, []));
        Assert.Equal(1m, SubjectiveFatigue.Rise(3m, [2m]));
        Assert.Equal(1m, SubjectiveFatigue.Rise(3m, [1m, 3m]));
        Assert.Equal(-1m, SubjectiveFatigue.Rise(1m, [2m]));
    }

    [Fact]
    public void TheFirstWeek_HasNoBaseline_SoItSaysNothing()
    {
        var (rise, previous) = SubjectiveFatigue.Rises(
            new Dictionary<int, decimal> { [1] = 4m },
            [1, 2, 3, 4],
            weekNumber: 1);

        Assert.Equal((0m, 0m), (rise, previous));
    }

    /// <summary>Druga nedelja ima osnovu (prvu), ali nema nedelju pre sebe koja bi je potvrdila.</summary>
    [Fact]
    public void TheSecondWeek_HasARise_ButNothingBeforeItToConfirmIt()
    {
        var (rise, previous) = SubjectiveFatigue.Rises(
            new Dictionary<int, decimal> { [1] = 2m, [2] = 3m },
            [1, 2, 3, 4],
            weekNumber: 2);

        Assert.Equal((1m, 0m), (rise, previous));
    }

    [Fact]
    public void TwoWeeksInARowThatRose_AreEachOtherConfirmation()
    {
        var (rise, previous) = SubjectiveFatigue.Rises(
            new Dictionary<int, decimal> { [1] = 2m, [2] = 3m, [3] = 3.5m },
            [1, 2, 3, 4],
            weekNumber: 3);

        // Treća: 3.5 nad prosekom 2.5 = 1.0. Druga: 3 nad 2 = 1.0.
        Assert.Equal((1m, 1m), (rise, previous));
    }

    /// <summary>
    /// Nedelja bez odgovora ne potvrđuje ništa, ma koliko ranije nedelje bile visoke: „prethodna" je prethodna
    /// trenažna nedelja, a ne poslednja koja je odgovorila.
    /// </summary>
    [Fact]
    public void AWeekWithoutAnswers_DoesNotConfirm_AndDoesNotBecomeThePreviousOne()
    {
        var (rise, previous) = SubjectiveFatigue.Rises(
            new Dictionary<int, decimal> { [1] = 2m, [2] = 3m, [4] = 3.5m },
            [1, 2, 3, 4],
            weekNumber: 4);

        // Treća nije odgovarala, pa je ranija porast ne potvrđuje.
        Assert.Equal(1.0m, rise);
        Assert.Equal(0m, previous);
    }

    /// <summary>Rasterećenje nije u spisku trenažnih nedelja: oseća se bolje u njemu, pa bi spustilo osnovu.</summary>
    [Fact]
    public void ADeloadWeek_DoesNotLowerTheBaseline()
    {
        var means = new Dictionary<int, decimal> { [1] = 3m, [2] = 1m, [3] = 3.5m };

        // Bez rasterećenja (nedelja 2) u spisku: osnova je 3, porast 0.5.
        var (withoutDeload, _) = SubjectiveFatigue.Rises(means, [1, 3], weekNumber: 3);
        // Sa njim bi osnova pala na 2, a porast bi bio 1.5: pogrešno velik.
        var (withDeload, _) = SubjectiveFatigue.Rises(means, [1, 2, 3], weekNumber: 3);

        Assert.Equal(0.5m, withoutDeload);
        Assert.Equal(1.5m, withDeload);
    }

    [Fact]
    public void NoAnswersAtAll_IsNoRiseAndNoConfirmation()
    {
        var (rise, previous) = SubjectiveFatigue.Rises(new Dictionary<int, decimal>(), [1, 2, 3], weekNumber: 3);

        Assert.Equal((0m, 0m), (rise, previous));
    }
}
