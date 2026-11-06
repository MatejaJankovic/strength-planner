using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Nov maksimum menja samo cilj koji je generator izveo iz maksimuma i koji vežbač još nije
/// počeo da radi. Ostale ciljeve piše progresija iz dignutih serija, ili se računaju pri
/// čitanju, pa njih nov zapis ili ne treba da dira ili već dira sam.
/// </summary>
public class GeneratedTargetTests
{
    [Theory]
    [InlineData(1, SessionStatus.Planned, true)]
    [InlineData(1, SessionStatus.InProgress, false)]
    [InlineData(1, SessionStatus.Completed, false)]
    [InlineData(1, SessionStatus.Skipped, false)]
    [InlineData(2, SessionStatus.Planned, false)]
    [InlineData(6, SessionStatus.Planned, false)]
    public void OnlyAWeekOneSessionNotYetStarted_FollowsTheMaximum(int weekNumber, SessionStatus status, bool expected)
    {
        Assert.Equal(expected, GeneratedTarget.FollowsTheMaximum(weekNumber, status));
    }
}
