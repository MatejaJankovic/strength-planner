using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Priručnik: <i>"Jedini volumen koji se računa je volumen koji sadrži mehaničku tenziju,
/// odnosno serije koje su odrađene do ili blizu otkaza"</i>, uz granicu na RIR 4.
/// </summary>
public class StimulativeVolumeTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.0)]
    [InlineData(3, 1.0)]
    public void CreditFor_CountsSetsNearFailureInFull(int rir, double expected)
    {
        Assert.Equal((decimal)expected, StimulativeVolume.CreditFor(rir, isFailure: false));
    }

    [Fact]
    public void CreditFor_HalvesTheSetAtTheOuterBound()
    {
        // RIR 4 je granica koju priručnik postavlja: još uvek nešto, ali ne puna serija.
        Assert.Equal(0.5m, StimulativeVolume.CreditFor(4, isFailure: false));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    public void CreditFor_DiscardsSetsTooFarFromFailure(int rir)
    {
        // Takva serija donosi zamor, ali ne i stimulus koji broj serija treba da meri.
        Assert.Equal(0m, StimulativeVolume.CreditFor(rir, isFailure: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    public void CreditFor_AlwaysCountsAFailedSetInFull(int rir)
    {
        // Serija izvučena do otkaza je po definiciji na otkazu — koji god RIR stajao uz
        // nju, dalje od otkaza ne može da bude.
        Assert.Equal(1m, StimulativeVolume.CreditFor(rir, isFailure: true));
    }

    [Fact]
    public void CreditFor_TakesTheSameViewOfFailureAsTheProgressionEngine()
    {
        var failed = new WorkingSet(5, 0, IsFailure: true);
        var easy = new WorkingSet(10, 5);

        Assert.Equal(1m, StimulativeVolume.CreditFor(failed));
        Assert.Equal(0m, StimulativeVolume.CreditFor(easy));
    }

    [Fact]
    public void CreditFor_MakesTwentyEasySetsWorthNothing()
    {
        // Ovo je slučaj zbog koga pravilo postoji: dvadeset serija daleko od otkaza je
        // ranije prijavljivalo "iznad MRV" i tražilo od korisnika da smanji volumen,
        // iako po priručniku nije uradio nijednu stimulativnu seriju.
        var week = Enumerable.Repeat(new WorkingSet(12, 5), 20).ToList();

        var stimulative = week.Sum(StimulativeVolume.CreditFor);

        Assert.Equal(0m, stimulative);
    }

    [Fact]
    public void CreditFor_LeavesAnOrdinaryHardWeekUnchanged()
    {
        // Nedelja odrađena po planu (RIR 1-2) mora da se broji isto kao ranije, inače bi
        // pravilo tiho oborilo volumen svim korisnicima.
        var week = new List<WorkingSet>
        {
            new(10, 1), new(10, 2), new(9, 1), new(8, 0), new(8, 2)
        };

        Assert.Equal(week.Count, week.Sum(StimulativeVolume.CreditFor));
    }
}
