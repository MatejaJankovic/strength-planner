namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// The load an exercise starts from when no logged session has spoken for it yet.
///
/// Two callers need exactly this: the generator, filling the first week of a block, and the
/// workout screen, for a week whose previous session has not been completed. They used to
/// disagree — the generator derived a load, and the screen said "no 1RM for this exercise"
/// even when a maximum was on file — so the rule lives in one place.
/// </summary>
public static class StartingLoad
{
    /// <summary>
    /// What to put on the bar, derived from a known maximum and the week's own prescription.
    ///
    /// Returns what is <b>added</b>, so a bodyweight exercise answers in belt kilograms.
    /// With no maximum at all, a bodyweight exercise still starts at a real number — zero
    /// added, which is "with your own mass" and is true — while an externally loaded one
    /// genuinely has nothing to say and returns null.
    /// </summary>
    public static decimal? FromOneRepMax(
        E1RmCalculator calculator,
        decimal? oneRepMaxKg,
        int repRangeMin,
        int targetRir,
        decimal bodyweightLoadKg,
        decimal weightStepKg)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        if (oneRepMaxKg is null)
        {
            return bodyweightLoadKg > 0 ? 0m : null;
        }

        return BodyweightLoad.AddedTarget(
            calculator.WorkingLoadFor(oneRepMaxKg.Value, repRangeMin, targetRir),
            bodyweightLoadKg,
            weightStepKg);
    }
}
