using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Kada nedelja prestaje da čeka.
///
/// Dok je „gotovo" značilo „svaki trening završen", jedan trening koji vežbač nikada neće
/// odraditi držao je nedelju otvorenom zauvek: nije se ocenjivala za umor, njen volumen
/// nije učio granice, a sledeći blok plana se nije generisao. Izmereno nad razvojnom bazom,
/// devet nedelja je stajalo baš tako — sa rupom, a kasnija nedelja već odrađena.
/// </summary>
public class SessionLifecycleTests
{
    [Theory]
    [InlineData(SessionStatus.Planned, false)]
    [InlineData(SessionStatus.InProgress, false)]
    [InlineData(SessionStatus.Completed, true)]
    [InlineData(SessionStatus.Skipped, true)]
    public void IsSettled_IsTrueOnlyWhenNothingMoreIsOwed(SessionStatus status, bool expected)
    {
        Assert.Equal(expected, SessionLifecycle.IsSettled(status));
        Assert.Equal(!expected, SessionLifecycle.IsPending(status));
    }

    /// <summary>
    /// Ovo je test protiv zaborava, ne protiv greške u računu: onog dana kada se doda nov
    /// status, on mora da bude svrstan u jednu od dve grupe. Bez ovoga bi nov status tiho
    /// ispao „nije završen", pa bi nedelje ponovo počele da stoje otvorene.
    /// </summary>
    [Fact]
    public void EveryStatus_IsEitherSettledOrPending()
    {
        foreach (var status in Enum.GetValues<SessionStatus>())
        {
            Assert.NotEqual(SessionLifecycle.IsSettled(status), SessionLifecycle.IsPending(status));
        }
    }

    /// <summary>
    /// Isti spisak koji upiti šalju bazi. Da su se razišli, kod bi nedelju smatrao gotovom
    /// a upit je ne bi našao — greška koju nijedna od dve strane sama ne vidi.
    /// </summary>
    [Fact]
    public void TheSetTheQueriesUse_MatchesThePredicate()
    {
        foreach (var status in Enum.GetValues<SessionStatus>())
        {
            Assert.Equal(SessionLifecycle.Settled.Contains(status), SessionLifecycle.IsSettled(status));
        }
    }

    [Theory]
    [InlineData(SessionStatus.Planned, true)]
    [InlineData(SessionStatus.InProgress, false)]
    [InlineData(SessionStatus.Completed, false)]
    [InlineData(SessionStatus.Skipped, false)]
    public void OnlyAnUntouchedSession_CanBeSkipped(SessionStatus status, bool expected)
    {
        // Trening koji je u toku ili završen nosi upisane serije. Preskakanje bi moralo da
        // odluči šta sa njima, a nijedan odgovor na to nije istinit o onome što se desilo.
        Assert.Equal(expected, SessionLifecycle.CanSkip(status));
    }

    [Theory]
    [InlineData(SessionStatus.Skipped, true)]
    [InlineData(SessionStatus.Planned, false)]
    [InlineData(SessionStatus.InProgress, false)]
    [InlineData(SessionStatus.Completed, false)]
    public void OnlyASkippedSession_CanGoBackOnThePlan(SessionStatus status, bool expected)
    {
        Assert.Equal(expected, SessionLifecycle.CanUnskip(status));
    }

    [Fact]
    public void AWeekIsOver_WhenNothingIsPending()
    {
        Assert.True(SessionLifecycle.IsOver([SessionStatus.Completed, SessionStatus.Completed]));
        Assert.True(SessionLifecycle.IsOver([SessionStatus.Completed, SessionStatus.Skipped]));

        // Ovo je tačno slučaj koji je ranije zaglavljivao blok.
        Assert.False(SessionLifecycle.IsOver([SessionStatus.Completed, SessionStatus.Planned]));
        Assert.False(SessionLifecycle.IsOver([SessionStatus.Skipped, SessionStatus.InProgress]));
    }

    /// <summary>
    /// Učenje granica je strože od zatvaranja nedelje, i to namerno. Nedelja sa preskočenim
    /// danom je uradila manje nego što je propisala, pa o tome koliko volumena vežbaču treba
    /// ne govori ništa — isto pravilo po kome ni signal snage bez poređenja ne pomera ništa.
    /// </summary>
    [Fact]
    public void AWeekWithAHole_ClosesButDoesNotTeachTheVolumeLimits()
    {
        var week = new[] { SessionStatus.Completed, SessionStatus.Completed, SessionStatus.Skipped };

        Assert.True(SessionLifecycle.IsOver(week));
        Assert.False(SessionLifecycle.TeachesVolume(week));

        // A nedelja u kojoj je sve odrađeno i dalje uči, kao i do sada.
        Assert.True(SessionLifecycle.TeachesVolume([SessionStatus.Completed, SessionStatus.Completed]));
    }
}
