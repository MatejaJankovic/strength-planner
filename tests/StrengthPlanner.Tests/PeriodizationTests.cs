using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Priručnik: <i>"Ne možeš isto trenirati svake nedelje i očekivati da napreduješ."</i>
/// Ovi testovi drže tri rasporeda razdvojena i unutar bezbednih granica.
/// </summary>
public class PeriodizationTests
{
    // Hipertrofija: 8-12 ponavljanja, RIR 1. Srednji nivo: 4 serije.
    private const int HypertrophyMin = 8;
    private const int HypertrophyMax = 12;
    private const int HypertrophyRir = 1;

    // Snaga: 3-6 ponavljanja, RIR 2.
    private const int StrengthMin = 3;
    private const int StrengthMax = 6;
    private const int StrengthRir = 2;

    private const int Sets = 4;

    private static IReadOnlyList<WeekPrescription> Hypertrophy(PeriodizationModel model) =>
        Periodization.ForBlock(model, HypertrophyMin, HypertrophyMax, HypertrophyRir, Sets);

    private static IReadOnlyList<WeekPrescription> Strength(PeriodizationModel model) =>
        Periodization.ForBlock(model, StrengthMin, StrengthMax, StrengthRir, Sets);

    [Theory]
    [InlineData(PeriodizationModel.Flat, 4)]
    [InlineData(PeriodizationModel.Linear, 6)]
    [InlineData(PeriodizationModel.Inverse, 6)]
    public void DurationWeeks_DependsOnTheModel(PeriodizationModel model, int expected)
    {
        Assert.Equal(expected, Periodization.DurationWeeks(model));
        Assert.Equal(expected, Periodization.ForBlock(model, 8, 12, 1, 4).Count);
    }

    [Fact]
    public void Flat_KeepsExactlyTheBehaviourTheSystemHadBeforeModelsExisted()
    {
        // Ravan blok je podrazumevani; ako se ovde nešto pomeri, promenili su se planovi
        // svih zatečenih korisnika.
        var weeks = Hypertrophy(PeriodizationModel.Flat);

        Assert.Equal(4, weeks.Count);

        foreach (var week in weeks.Take(3))
        {
            Assert.False(week.IsDeload);
            Assert.Equal(Sets, week.Sets);
            Assert.Equal(HypertrophyMin, week.RepRangeMin);
            Assert.Equal(HypertrophyMax, week.RepRangeMax);
            Assert.Equal(HypertrophyRir, week.TargetRir);
        }

        var deload = weeks[3];
        Assert.True(deload.IsDeload);
        Assert.Equal(2, deload.Sets);
        Assert.Equal(HypertrophyMin, deload.RepRangeMin);
        Assert.Equal(HypertrophyMax, deload.RepRangeMax);
        Assert.Equal(HypertrophyRir, deload.TargetRir);
    }

    [Fact]
    public void Linear_StartsWithVolumeAndEndsWithIntensity()
    {
        var weeks = Hypertrophy(PeriodizationModel.Linear);

        // Prva nedelja: više ponavljanja, lakše serije, više serija.
        Assert.Equal(11, weeks[0].RepRangeMin);
        Assert.Equal(15, weeks[0].RepRangeMax);
        Assert.Equal(HypertrophyRir + 1, weeks[0].TargetRir);
        Assert.Equal(Sets + 1, weeks[0].Sets);

        // Peta nedelja: manje ponavljanja, bliže otkazu, manje serija.
        Assert.Equal(6, weeks[4].RepRangeMin);
        Assert.Equal(10, weeks[4].RepRangeMax);
        Assert.Equal(HypertrophyRir - 1, weeks[4].TargetRir);
        Assert.Equal(Sets - 1, weeks[4].Sets);

        Assert.True(weeks[5].IsDeload);
    }

    [Fact]
    public void Inverse_IsTheMirrorOfLinear()
    {
        var linear = Hypertrophy(PeriodizationModel.Linear);
        var inverse = Hypertrophy(PeriodizationModel.Inverse);

        // Obrnut model počinje tamo gde linearni završava i obrnuto.
        Assert.Equal(linear[4].RepRangeMin, inverse[0].RepRangeMin);
        Assert.Equal(linear[4].RepRangeMax, inverse[0].RepRangeMax);
        Assert.Equal(linear[0].RepRangeMin, inverse[4].RepRangeMin);
        Assert.Equal(linear[0].RepRangeMax, inverse[4].RepRangeMax);
    }

    [Theory]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void PeriodizedBlocks_LowerTheRirAsTheBlockGoesOn(PeriodizationModel model)
    {
        // Zamor raste kroz blok, pa se serije vode sve bliže otkazu — do deload-a.
        var training = Hypertrophy(model).Where(week => !week.IsDeload).ToList();

        for (var index = 1; index < training.Count; index++)
        {
            Assert.True(
                training[index].TargetRir <= training[index - 1].TargetRir,
                $"Nedelja {training[index].WeekNumber}: RIR je porastao.");
        }
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void EveryBlock_HasExactlyOneDeloadAndItIsLast(PeriodizationModel model)
    {
        var weeks = Hypertrophy(model);

        Assert.Single(weeks.Where(week => week.IsDeload));
        Assert.True(weeks[^1].IsDeload);
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void EveryWeek_StaysInsideSafeBounds(PeriodizationModel model)
    {
        foreach (var weeks in new[] { Hypertrophy(model), Strength(model) })
        {
            foreach (var week in weeks)
            {
                Assert.InRange(week.RepRangeMin, Periodization.MinReps, Periodization.MaxReps);
                Assert.InRange(week.RepRangeMax, week.RepRangeMin + 1, Periodization.MaxReps);
                Assert.InRange(week.TargetRir, Periodization.MinRir, Periodization.MaxRir);
                Assert.True(week.Sets >= 1, $"Nedelja {week.WeekNumber}: {week.Sets} serija.");
            }
        }
    }

    [Fact]
    public void Strength_DoesNotDropBelowThreeReps_AndLeansOnRirInstead()
    {
        // Blok snage već stoji na 3-6 ponavljanja; niže od tri se ne ide, pa fazu
        // intenziteta nosi RIR, a ne još kraći opseg.
        var weeks = Strength(PeriodizationModel.Linear);
        var intensityWeek = weeks[4];

        Assert.Equal(Periodization.MinReps, intensityWeek.RepRangeMin);
        Assert.True(intensityWeek.TargetRir < StrengthRir);
    }

    [Fact]
    public void DeloadSets_HalveTheWeekButNeverFallBelowOne()
    {
        Assert.Equal(2, Periodization.DeloadSets(4));
        Assert.Equal(2, Periodization.DeloadSets(3));
        Assert.Equal(1, Periodization.DeloadSets(1));
    }

    [Fact]
    public void DeloadWeek_KeepsTheGoalRepRangeAndRir()
    {
        // Rasterećenje nosi opterećenje (90% stvarnog) i polovinu serija; menjanje i
        // opsega bi promenilo i sam pokret, a ne samo njegovu težinu.
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            var deload = Hypertrophy(model)[^1];

            Assert.Equal(HypertrophyMin, deload.RepRangeMin);
            Assert.Equal(HypertrophyMax, deload.RepRangeMax);
            Assert.Equal(HypertrophyRir, deload.TargetRir);
        }
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void ForWeek_RejectsAWeekOutsideTheBlock(PeriodizationModel model)
    {
        var duration = Periodization.DurationWeeks(model);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Periodization.ForWeek(model, 0, 8, 12, 1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Periodization.ForWeek(model, duration + 1, 8, 12, 1, 4));
    }

    [Fact]
    public void ForWeek_NumbersWeeksFromOneInOrder()
    {
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            var weeks = Hypertrophy(model);

            Assert.Equal(
                Enumerable.Range(1, weeks.Count),
                weeks.Select(week => week.WeekNumber));
        }
    }

    [Fact]
    public void PeriodizedBlocks_ActuallyDifferFromWeekToWeek()
    {
        // Bez ovoga bi model mogao da se "primeni" a da ne promeni nijedan propis —
        // što je upravo zamerka od koje je ova grana krenula.
        foreach (var model in new[] { PeriodizationModel.Linear, PeriodizationModel.Inverse })
        {
            var distinct = Hypertrophy(model)
                .Select(week => (week.Sets, week.RepRangeMin, week.RepRangeMax, week.TargetRir))
                .Distinct()
                .Count();

            Assert.True(distinct >= 4, $"{model}: samo {distinct} različitih propisa u bloku.");
        }
    }
}
