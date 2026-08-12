using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

public class MacrocyclePlannerTests
{
    [Fact]
    public void AlternatingGoals_StartsFromTheChosenGoal()
    {
        var goals = MacrocyclePlanner.AlternatingGoals(4, Goal.Hypertrophy);

        Assert.Equal(
            new[] { Goal.Hypertrophy, Goal.Strength, Goal.Hypertrophy, Goal.Strength },
            goals);
    }

    [Fact]
    public void AlternatingGoals_AlsoStartsFromStrength()
    {
        var goals = MacrocyclePlanner.AlternatingGoals(3, Goal.Strength);

        Assert.Equal(new[] { Goal.Strength, Goal.Hypertrophy, Goal.Strength }, goals);
    }

    [Fact]
    public void AlternatingGoals_ReturnsASingleBlockPlanUnchanged()
    {
        // Pojedinačan mezociklus je plan sa jednim blokom — bez posebnog slučaja.
        var goals = MacrocyclePlanner.AlternatingGoals(1, Goal.Hypertrophy);

        Assert.Equal(new[] { Goal.Hypertrophy }, goals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void AlternatingGoals_RejectsBlockCountsOutsideTheSupportedRange(int blockCount)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MacrocyclePlanner.AlternatingGoals(blockCount, Goal.Hypertrophy));

        Assert.Equal("blockCount", exception.ParamName);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(6, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    public void IsValidBlockCount_MatchesTheDocumentedRange(int blockCount, bool expected)
    {
        Assert.Equal(expected, MacrocyclePlanner.IsValidBlockCount(blockCount));
    }

    [Fact]
    public void AlternatingGoals_NeverRepeatsTheSameGoalTwiceInARow()
    {
        for (var blockCount = MacrocyclePlanner.MinBlocks; blockCount <= MacrocyclePlanner.MaxBlocks; blockCount++)
        {
            var goals = MacrocyclePlanner.AlternatingGoals(blockCount, Goal.Hypertrophy);

            for (var index = 1; index < goals.Count; index++)
            {
                Assert.NotEqual(goals[index - 1], goals[index]);
            }
        }
    }
}
