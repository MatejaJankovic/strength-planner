using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Propis vežbe u jednoj nedelji. Generator ga je radio ugrađen u sebi, a dodavanje vežbe u blok koji teče
/// traži iste brojeve, pa su obe strane svedene na jednu funkciju (A3).
/// </summary>
public class ExercisePrescriptionsTests
{
    public static TheoryData<Goal, ExerciseType, bool, PeriodizationModel, ExperienceLevel> Grid()
    {
        var data = new TheoryData<Goal, ExerciseType, bool, PeriodizationModel, ExperienceLevel>();

        foreach (var goal in Enum.GetValues<Goal>())
        foreach (var type in Enum.GetValues<ExerciseType>())
        foreach (var suitsLowReps in new[] { true, false })
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            data.Add(goal, type, suitsLowReps, model, level);
        }

        return data;
    }

    /// <summary>
    /// Stari račun generatora, prepisan ovde kao proveravač: opseg iz cilja i tipa vežbe, RIR iz cilja, serije
    /// iz nivoa, a pomeraj iz modela i nedelje.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grid))]
    public void ItReproducesWhatTheGeneratorUsedToDoInline(
        Goal goal,
        ExerciseType type,
        bool suitsLowReps,
        PeriodizationModel model,
        ExperienceLevel level)
    {
        var settings = GoalPrescriptions.ForExercise(goal, type, suitsLowReps);

        for (var week = 1; week <= Periodization.DurationWeeks(model); week++)
        {
            var expected = Periodization.ForWeek(
                model,
                level,
                week,
                settings.RepRangeMin,
                settings.RepRangeMax,
                GoalPrescriptions.ForGoal(goal).TargetRir,
                ExperienceProgramming.StartingSetsPerExercise(level));

            var actual = ExercisePrescriptions.For(goal, type, suitsLowReps, model, level, week);

            Assert.Equal(expected, actual.Week);
            Assert.Equal(settings.RepRangeMin, actual.BaseRepRangeMin);
            Assert.Equal(settings.RepRangeMax, actual.BaseRepRangeMax);
        }
    }

    /// <summary>
    /// Lični šablon kaže svoj opseg i svoje serije, i ostaju njegovi kroz ceo blok; pomeraj ide od njih.
    /// </summary>
    [Fact]
    public void ATemplateRangeAndSets_ReplaceTheDerivedOnes_AndStayTheBase()
    {
        var model = PeriodizationModel.LinearRising;
        var level = ExperienceLevel.Intermediate;

        for (var week = 1; week <= Periodization.DurationWeeks(model); week++)
        {
            var actual = ExercisePrescriptions.For(
                Goal.Hypertrophy,
                ExerciseType.Compound,
                true,
                model,
                level,
                week,
                templateRepRangeMin: 5,
                templateRepRangeMax: 5,
                templateSets: 5);

            Assert.Equal(5, actual.BaseRepRangeMin);
            Assert.Equal(5, actual.BaseRepRangeMax);
            Assert.Equal(
                Periodization.ForWeek(model, level, week, 5, 5, GoalPrescriptions.ForGoal(Goal.Hypertrophy).TargetRir, 5),
                actual.Week);
        }
    }

    /// <summary>
    /// Deload polovi polazni broj serija, a za vežbu koja u osnovnoj nedelji nema red taj broj se izvodi iz
    /// propisa nedelje (<see cref="Periodization.BaseSetsFrom"/>). To važi samo ako se propis te funkcije može
    /// obrnuti: dodata vežba se u svakoj nedelji koja nije deload izvodi nazad na broj serija koji nivo propisuje.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grid))]
    public void TheBaseSetsOfAnAddedExercise_CanBeRecoveredFromAnyTrainingWeek(
        Goal goal,
        ExerciseType type,
        bool suitsLowReps,
        PeriodizationModel model,
        ExperienceLevel level)
    {
        var startingSets = ExperienceProgramming.StartingSetsPerExercise(level);

        for (var week = 1; week <= Periodization.DurationWeeks(model); week++)
        {
            // Nedelja koja je deload po obliku modela nema šta da se izvodi: tu se serije prepolove.
            if (Periodization.ForWeek(model, week, 8, 12, 2, startingSets).IsDeload)
            {
                continue;
            }

            var prescribed = ExercisePrescriptions.For(goal, type, suitsLowReps, model, level, week);
            var recovered = Periodization.BaseSetsFrom(model, week, prescribed.Week.Sets, prescribed.BaseRepRangeMax);

            Assert.Equal(startingSets, recovered);
        }
    }
}
