using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

public class EquipmentWeightStepTests
{
    [Theory]
    [InlineData("Barbell", 2.5)]
    [InlineData("Dumbbell", 2.0)]
    [InlineData("Machine", 5.0)]
    [InlineData("Cable", 2.5)]
    [InlineData("Bodyweight", 1.0)]
    public void ForEquipment_ReturnsStepMatchingEquipment(string equipment, double expected)
    {
        var step = EquipmentWeightStep.ForEquipment(equipment);

        Assert.Equal((decimal)expected, step);
    }

    [Theory]
    [InlineData("machine")]
    [InlineData("  Machine  ")]
    public void ForEquipment_IgnoresCaseAndSurroundingWhitespace(string equipment)
    {
        var step = EquipmentWeightStep.ForEquipment(equipment);

        Assert.Equal(5.0m, step);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Kettlebell")]
    public void ForEquipment_FallsBackToGlobalDefaultForUnknownEquipment(string? equipment)
    {
        var step = EquipmentWeightStep.ForEquipment(equipment);

        Assert.Equal(TrainingConstants.WeightStepKg, step);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(2.0, true)]
    [InlineData(10.0, true)]
    [InlineData(0.4, false)]
    [InlineData(10.5, false)]
    [InlineData(0.0, false)]
    [InlineData(-2.5, false)]
    public void IsValidStep_AcceptsOnlyStepsInsideAllowedRange(double stepKg, bool expected)
    {
        var isValid = EquipmentWeightStep.IsValidStep((decimal)stepKg);

        Assert.Equal(expected, isValid);
    }
}
