using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Nov maksimum menja samo cilj koji je generator izveo iz maksimuma i koji vežbač još nije
/// dirnuo. Ostale ciljeve upisuje završen trening, ili se računaju pri čitanju, pa njih nov
/// zapis ili ne treba da dira ili već dira sam.
/// </summary>
public class GeneratedTargetTests
{
    [Theory]
    [InlineData(1, SessionStatus.Planned, false, true)]
    [InlineData(1, SessionStatus.Skipped, false, true)]
    [InlineData(1, SessionStatus.Planned, true, false)]
    [InlineData(1, SessionStatus.InProgress, false, false)]
    [InlineData(1, SessionStatus.Completed, false, false)]
    [InlineData(2, SessionStatus.Planned, false, false)]
    [InlineData(6, SessionStatus.Planned, false, false)]
    public void OnlyAnUntouchedWeekOneSession_FollowsTheMaximum(
        int weekNumber,
        SessionStatus status,
        bool hasLoggedSets,
        bool expected)
    {
        Assert.Equal(expected, GeneratedTarget.FollowsTheMaximum(weekNumber, status, hasLoggedSets));
    }
}
