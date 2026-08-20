namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Default load increment per equipment type. A single global 2.5 kg step is too coarse
/// for dumbbells and too fine for most machine stacks, so the smallest realistically
/// available jump is derived from the equipment the exercise is performed with.
/// </summary>
public static class EquipmentWeightStep
{
    /// <summary>Smallest step the system will ever use, in kilograms.</summary>
    public const decimal MinStepKg = 0.5m;

    /// <summary>Largest step the system will ever use, in kilograms.</summary>
    public const decimal MaxStepKg = 10m;

    // Barbell: 2 x 1.25 kg plates. Dumbbell: racks usually move in 2 kg pairs.
    // Machine/cable: fixed stack increments. Bodyweight: added load on a belt.
    private static readonly IReadOnlyDictionary<string, decimal> StepsByEquipment =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Barbell"] = 2.5m,
            ["Dumbbell"] = 2m,
            ["Machine"] = 5m,
            ["Cable"] = 2.5m,
            ["Bodyweight"] = 1m
        };

    /// <summary>
    /// Returns the default step for an equipment name, falling back to the global
    /// default for equipment the catalog does not know (for example custom exercises).
    /// </summary>
    public static decimal ForEquipment(string? equipment)
    {
        if (equipment is not null && StepsByEquipment.TryGetValue(equipment.Trim(), out var step))
        {
            return step;
        }

        return TrainingConstants.WeightStepKg;
    }

    /// <summary>
    /// Equipment names this class knows a step for. Exposed so callers that need to
    /// validate an equipment string (for example a seed-data test) check against the same
    /// set <see cref="ForEquipment"/> actually uses, instead of keeping a second copy that
    /// can drift out of sync with it.
    /// </summary>
    public static IReadOnlyCollection<string> RecognizedEquipment { get; } = StepsByEquipment.Keys.ToList();

    /// <summary>Returns true when the value is a usable manual override.</summary>
    public static bool IsValidStep(decimal stepKg)
    {
        return stepKg >= MinStepKg && stepKg <= MaxStepKg;
    }

    /// <summary>
    /// Snaps an override to the two decimals the column actually stores, so the value
    /// returned to the client is the value that will be read back from the database.
    /// </summary>
    public static decimal Normalize(decimal stepKg)
    {
        return Math.Round(stepKg, 2, MidpointRounding.AwayFromZero);
    }
}
