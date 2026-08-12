using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Priručnik razdvaja tri nivoa vežbača konkretnim brojevima i zaključuje:
/// <i>"napredni vežbač bi pregoreo od treninga početnika"</i>.
/// </summary>
public class ExperienceProgrammingTests
{
    [Fact]
    public void MaxCompoundsPerSession_FallsAsExperienceRises()
    {
        // Složene vežbe nose najveći stimulus ali i najviše zamora, pa ih napredan
        // vežbač — koji ih radi najteže — mora imati najmanje po treningu.
        Assert.Equal(3, ExperienceProgramming.MaxCompoundsPerSession(ExperienceLevel.Beginner));
        Assert.Equal(2, ExperienceProgramming.MaxCompoundsPerSession(ExperienceLevel.Intermediate));
        Assert.Equal(1, ExperienceProgramming.MaxCompoundsPerSession(ExperienceLevel.Advanced));
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0.8)]
    [InlineData(ExperienceLevel.Intermediate, 1.0)]
    [InlineData(ExperienceLevel.Advanced, 1.2)]
    public void LandmarkScale_MovesTheWholeBandWithExperience(ExperienceLevel level, double expected)
    {
        Assert.Equal((decimal)expected, ExperienceProgramming.LandmarkScale(level));
    }

    [Fact]
    public void ScaleLandmarks_LowersTheWholeBandForABeginner()
    {
        // Chest seed 10/16/22 puta 0.8.
        var seed = new VolumeLandmarkValues(10, 16, 22);

        var scaled = ExperienceProgramming.ScaleLandmarks(seed, ExperienceLevel.Beginner);

        Assert.Equal(8, scaled.Mev);
        Assert.Equal(13, scaled.Mav);
        Assert.Equal(18, scaled.Mrv);
    }

    [Fact]
    public void ScaleLandmarks_RaisesTheWholeBandForAnAdvancedLifter()
    {
        var seed = new VolumeLandmarkValues(10, 16, 22);

        var scaled = ExperienceProgramming.ScaleLandmarks(seed, ExperienceLevel.Advanced);

        Assert.Equal(12, scaled.Mev);
        Assert.Equal(19, scaled.Mav);
        Assert.Equal(26, scaled.Mrv);
    }

    [Fact]
    public void ScaleLandmarks_LeavesTheIntermediateBandExactlyAsSeeded()
    {
        // Srednji nivo je referenca; skaliranje ne sme tiho da pomeri objavljene vrednosti.
        var seed = new VolumeLandmarkValues(10, 16, 22);

        Assert.Equal(seed, ExperienceProgramming.ScaleLandmarks(seed, ExperienceLevel.Intermediate));
    }

    [Fact]
    public void ScaleLandmarks_KeepsTheOrderEvenWhenRoundingWouldCollapseTheBand()
    {
        // Uzak pojas pomnožen sa 0.8 ume da se sruči u istu vrednost; redosled se
        // obnavlja, a ne pretpostavlja — inače bi upis pao na CHECK ograničenju.
        var narrow = new VolumeLandmarkValues(2, 3, 4);

        var scaled = ExperienceProgramming.ScaleLandmarks(narrow, ExperienceLevel.Beginner);

        Assert.True(scaled.Mev >= 1);
        Assert.True(scaled.Mev < scaled.Mav, $"MEV {scaled.Mev} nije ispod MAV {scaled.Mav}.");
        Assert.True(scaled.Mav < scaled.Mrv, $"MAV {scaled.Mav} nije ispod MRV {scaled.Mrv}.");
    }

    [Fact]
    public void DeloadThreshold_IsAbsentForBeginnersAndStricterWithExperience()
    {
        // Početniku umor ne povlači deload ranije — ostaje mu samo planirani na kraju bloka.
        Assert.Null(ExperienceProgramming.DeloadThreshold(ExperienceLevel.Beginner));
        Assert.Equal(0.60m, ExperienceProgramming.DeloadThreshold(ExperienceLevel.Intermediate));
        Assert.Equal(0.50m, ExperienceProgramming.DeloadThreshold(ExperienceLevel.Advanced));
    }

    [Fact]
    public void DeloadThreshold_MatchesTheEvaluatorDefaultForTheMiddleLevel()
    {
        // Srednji nivo mora da zadrži ponašanje koje je sistem imao pre ove izmene.
        Assert.Equal(
            FatigueEvaluator.DeloadThreshold,
            ExperienceProgramming.DeloadThreshold(ExperienceLevel.Intermediate));
    }
}
