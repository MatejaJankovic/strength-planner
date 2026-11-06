using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Kalibraciona serija (A1): u poslednjoj trenažnoj nedelji bloka prva serija glavnog
/// dizanja treninga ide do otkaza, za srednji i napredni nivo.
/// </summary>
public class CalibrationSetTests
{
    [Theory]
    [InlineData(ExperienceLevel.Intermediate, true)]
    [InlineData(ExperienceLevel.Advanced, true)]
    [InlineData(ExperienceLevel.Beginner, false)]
    public void AppliesOnlyFromTheIntermediateLevel(ExperienceLevel level, bool expected)
    {
        Assert.Equal(expected, CalibrationSet.AppliesTo(level, true, 1, ExerciseType.Compound, true, 6, 1));
    }

    [Fact]
    public void AppliesOnlyToTheFirstExercise_InTheLastTrainingWeek()
    {
        Assert.False(CalibrationSet.AppliesTo(ExperienceLevel.Advanced, false, 1, ExerciseType.Compound, true, 6, 1));
        Assert.False(CalibrationSet.AppliesTo(ExperienceLevel.Advanced, true, 2, ExerciseType.Compound, true, 6, 1));
    }

    [Fact]
    public void AppliesOnlyToACompoundThatSuitsLowReps()
    {
        // Izolacija ili iskorak: maksimum tu nije mera koju plan koristi.
        Assert.False(CalibrationSet.AppliesTo(ExperienceLevel.Advanced, true, 1, ExerciseType.Isolation, true, 6, 1));
        Assert.False(CalibrationSet.AppliesTo(ExperienceLevel.Advanced, true, 1, ExerciseType.Compound, false, 6, 1));
    }

    /// <summary>
    /// Na propisanoj težini serija pada oko vrha opsega plus ciljni RIR. Iznad Epley granice
    /// procena se ne uzima, pa ni kalibracija nema šta da izmeri: poslednja nedelja ravnog i
    /// obrnutog bloka hipertrofije (8-12 uz RIR 1; ciljni RIR ne ide ispod 1) bi stala oko 13.
    /// Peta nedelja linearnog bloka (6-10 uz RIR 1) staje u granicu, kao i svaka nedelja bloka
    /// snage.
    /// </summary>
    [Theory]
    [InlineData(12, 1, false)]
    [InlineData(12, 0, true)]
    [InlineData(10, 0, true)]
    [InlineData(6, 2, true)]
    [InlineData(11, 2, false)]
    public void AppliesOnlyWhereTheSetToFailureCanBeRead(int repRangeMax, int targetRir, bool expected)
    {
        Assert.Equal(
            expected,
            CalibrationSet.AppliesTo(ExperienceLevel.Intermediate, true, 1, ExerciseType.Compound, true, repRangeMax, targetRir));
    }

    [Fact]
    public void TheLastTrainingWeek_IsTheLastWeekThatIsNotADeload()
    {
        (int, bool)[] plannedDeload = [(1, false), (2, false), (3, false), (4, true)];
        Assert.True(CalibrationSet.IsLastTrainingWeek(3, false, plannedDeload));
        Assert.False(CalibrationSet.IsLastTrainingWeek(2, false, plannedDeload));
        Assert.False(CalibrationSet.IsLastTrainingWeek(4, true, plannedDeload));

        // Auto-deload u drugoj nedelji je oslobodio planirani: blok se završava trenažnom
        // nedeljom, i kalibracija ide u nju.
        (int, bool)[] released = [(1, false), (2, true), (3, false), (4, false)];
        Assert.True(CalibrationSet.IsLastTrainingWeek(4, false, released));
        Assert.False(CalibrationSet.IsLastTrainingWeek(3, false, released));
    }

    /// <summary>
    /// Pravilo mora da pogodi poslednju trenažnu nedelju svakog bloka snage i linearnog bloka
    /// hipertrofije, koji čarobnjak predlaže za svaki blok (#90): inače bi kalibracija postojala
    /// samo na papiru. Meri se nad stvarnim propisom te nedelje.
    /// </summary>
    [Theory]
    [InlineData(PeriodizationModel.Flat, Goal.Strength, true)]
    [InlineData(PeriodizationModel.Flat, Goal.Hypertrophy, false)]
    [InlineData(PeriodizationModel.LinearRising, Goal.Hypertrophy, true)]
    [InlineData(PeriodizationModel.LinearRising, Goal.Strength, true)]
    [InlineData(PeriodizationModel.Inverse, Goal.Hypertrophy, false)]
    [InlineData(PeriodizationModel.Inverse, Goal.Strength, true)]
    public void TheLastTrainingWeekOfABlock_CanBeCalibrated_WhereTheRuleSays(
        PeriodizationModel model,
        Goal goal,
        bool expected)
    {
        var settings = GoalPrescriptions.ForGoal(goal);
        var lastTrainingWeek = Enumerable.Range(1, Periodization.DurationWeeks(model))
            .Last(week => !Periodization.ForWeek(model, week, settings.RepRangeMin, settings.RepRangeMax, settings.TargetRir, 4).IsDeload);
        var week = Periodization.ForWeek(model, lastTrainingWeek, settings.RepRangeMin, settings.RepRangeMax, settings.TargetRir, 4);

        Assert.Equal(
            expected,
            CalibrationSet.AppliesTo(ExperienceLevel.Intermediate, true, 1, ExerciseType.Compound, true, week.RepRangeMax, week.TargetRir));
    }
}
