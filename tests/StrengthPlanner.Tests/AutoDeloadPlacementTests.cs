using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Gde umor sme da povuče deload napred. Pravilo je do sada živelo u upitu servisa, a
/// servis nema test - pa ga nije ni imalo ništa što bi primetilo drugi auto-deload u istom
/// bloku.
/// </summary>
public class AutoDeloadPlacementTests
{
    /// <summary>Šestonedeljni blok: pet trenažnih nedelja i planirani deload na kraju.</summary>
    private static List<BlockWeekState> SixWeekBlock()
    {
        return Enumerable.Range(1, 6)
            .Select(number => new BlockWeekState(number, IsDeload: number == 6, IsAutoDeload: false, HasStarted: false))
            .ToList();
    }

    [Fact]
    public void TheWeekAfterTheEvaluatedOne_BecomesTheDeload()
    {
        Assert.Equal(3, AutoDeloadPlacement.NextWeek(SixWeekBlock(), 2));
    }

    [Fact]
    public void ThePlannedDeload_IsNotPulledOntoItself()
    {
        Assert.Null(AutoDeloadPlacement.NextWeek(SixWeekBlock(), 5));
    }

    [Fact]
    public void TheLastWeek_HasNoWeekAfterIt()
    {
        Assert.Null(AutoDeloadPlacement.NextWeek(SixWeekBlock(), 6));
    }

    [Fact]
    public void AWeekThatHasStarted_IsNotRewritten()
    {
        var weeks = SixWeekBlock();
        weeks[2] = weeks[2] with { HasStarted = true };

        Assert.Null(AutoDeloadPlacement.NextWeek(weeks, 2));
    }

    /// <summary>
    /// Nalaz iz review-a PR #90: posle auto-deload-a u nedelji 3 planirani deload se
    /// oslobađa, pa nedelja 6 postaje trenažna. Umor posle nedelje 5 ju je onda pretvarao u
    /// drugi deload - a pošto je osnovna nedelja (3) već bila deload, polazni broj serija se
    /// izvodio iz oblika nedelje 6, koji je deload i nema pomeraj, i servis je pucao.
    /// </summary>
    [Fact]
    public void ABlockThatAlreadyPulledADeloadForward_DoesNotPullASecond()
    {
        var weeks = SixWeekBlock();
        weeks[2] = weeks[2] with { IsDeload = true, IsAutoDeload = true };
        weeks[5] = weeks[5] with { IsDeload = false };

        Assert.Null(AutoDeloadPlacement.NextWeek(weeks, 5));
    }

    [Fact]
    public void TheRuleDoesNotDependOnTheOrderTheWeeksArriveIn()
    {
        var weeks = SixWeekBlock();
        weeks.Reverse();

        Assert.Equal(4, AutoDeloadPlacement.NextWeek(weeks, 3));
    }
}
