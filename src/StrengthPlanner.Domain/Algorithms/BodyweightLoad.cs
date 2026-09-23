namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// How much a bodyweight exercise actually loads, and how that converts back to the number
/// the lifter puts on a belt.
///
/// A pull-up was logged as 0 kg, so everything downstream read it as no load at all: the
/// estimated maximum came out 0, tonnage counted nothing, and progression proposed "+1 kg"
/// on a set the lifter had never loaded. The body is the load, and the profile already
/// carries its weight — it simply never reached the training rules.
///
/// The model is one number per exercise: the share of body mass the movement lifts. A
/// pull-up moves the whole body, a push-up roughly two thirds of it, a split squat most of
/// it. The share is an estimate and does not need to be exact: at 80 kg body mass a 10%
/// error in the share moves the portion by about 7 kg, and a 3% correction on that is
/// 0.2 kg — well inside one plate. What it must not be is zero, which is what it was.
/// </summary>
public static class BodyweightLoad
{
    /// <summary>Equipment name whose exercises carry a share of body mass.</summary>
    public const string BodyweightEquipment = "Bodyweight";

    /// <summary>Largest share an exercise may carry: the whole body.</summary>
    public const decimal MaxShare = 1m;

    /// <summary>
    /// Kilograms of body mass a set of this exercise moves, rounded to the two decimals the
    /// column stores. Zero for everything loaded externally, and for a profile without a
    /// recorded body mass.
    /// </summary>
    public static decimal PortionKg(decimal bodyweightKg, decimal share)
    {
        if (bodyweightKg <= 0 || share <= 0)
        {
            return 0m;
        }

        return Math.Round(bodyweightKg * Math.Min(share, MaxShare), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The added load to propose so that the total lands as close as possible to
    /// <paramref name="rawTotalKg"/>.
    ///
    /// Rounded in <b>added</b> space, because that is where the step exists: plates go on the
    /// belt, not on the body. Never negative — when the wanted total is below body mass
    /// there is nothing to take off, and progression continues through reps
    /// (<see cref="IsAtBodyweightFloor"/>).
    /// </summary>
    public static decimal AddedTarget(decimal rawTotalKg, decimal portionKg, decimal stepKg)
    {
        return Math.Max(0m, WeightMath.RoundToStep(rawTotalKg - portionKg, stepKg));
    }

    /// <summary>
    /// True when the wanted total is lighter than the body itself, so the proposal is body
    /// mass alone.
    /// </summary>
    public static bool IsAtBodyweightFloor(decimal rawTotalKg, decimal portionKg)
    {
        return portionKg > 0 && rawTotalKg < portionKg;
    }

    /// <summary>Whether an exercise is one the lifter loads with their own body.</summary>
    public static bool IsBodyweightExercise(string? equipment)
    {
        return string.Equals(equipment?.Trim(), BodyweightEquipment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a share is inside the range an exercise may carry.</summary>
    public static bool IsValidShare(decimal share)
    {
        return share >= 0m && share <= MaxShare;
    }
}
