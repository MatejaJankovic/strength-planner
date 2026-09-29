namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Whether one load step fits inside a rep range, and how many reps it takes to earn the
/// step when it does not.
///
/// Double progression adds one step once every set reaches the top of the range, and the
/// next session starts again at the bottom. That reset pays for the step, and by Epley it
/// pays for a lot: a lifter at the top of 8-12 with one rep in reserve can take about 13%
/// more load and still complete 8 reps. Every barbell step on a real load fits. A 2 kg
/// dumbbell step on an 8 kg lateral raise is 25%, and it did not fit: the lifter who did
/// 3 x 12 at 8 kg was handed 10 kg, where Epley leaves about three reps at RIR 1 - five
/// below the floor of the range the session prescribes. Measured on the unchanged engine
/// before this class existed.
///
/// The test is deliberately the loose one: a step fits when the floor of the range stays
/// reachable <b>at all</b> after it, even at failure - not when it stays reachable at the
/// target RIR. The strict version was tried first and failed 13 of 613 existing tests,
/// nine more than this one, all for the same good reason: a narrow week (11-12 at RIR 2)
/// or a fixed target (5 x 5) absorbs only 2-6% at the target RIR, so even 100 kg on a
/// 2.5 kg bar would have stopped stepping. There the reserve is
/// what absorbs the step, and spending one rep of it is ordinary progression; handing a
/// load whose floor lies beyond failure is not.
///
/// When the step does not fit, the load stays and the rep target moves instead: the step
/// is earned at the rep count where the reset does pay for it (17 in that example). The
/// target is a pure function of the prescription and the load, so the progression engine
/// and the workout screen compute it the same way and nothing about it has to be stored.
/// </summary>
public static class StepAbsorption
{
    /// <summary>
    /// Reps every set has to reach before the next session gets one step more:
    /// <paramref name="repRangeMax"/> whenever the step fits the range, otherwise the
    /// smallest rep count r for which (30 + r + rir) / (30 + min) covers
    /// (load + step) / load - a lifter at r reps with the target reserve can still complete
    /// the floor of the range after the step.
    /// </summary>
    /// <param name="totalLoadKg">Load lifted, body portion included.</param>
    /// <param name="stepKg">Smallest load increment of the exercise.</param>
    /// <param name="repRangeMin">Floor of the prescribed range.</param>
    /// <param name="repRangeMax">Top of the prescribed range.</param>
    /// <param name="targetRir">Prescribed reps in reserve.</param>
    public static int RepsToEarnStep(
        decimal totalLoadKg,
        decimal stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir)
    {
        if (totalLoadKg <= 0 || stepKg <= 0)
        {
            return repRangeMax;
        }

        // Uporedjuje se unakrsnim mnozenjem, bez deljenja: kolicnik (u + s) / u ume da ne
        // bude konacan decimalni broj, pa bi zaokruzivanje moglo da doda ponavljanje vise.
        // Dno opsega posle koraka mora da ostane izvodljivo bar do otkaza (RIR 0).
        var floorAtFailure = TrainingConstants.EpleyRepDivisor + repRangeMin;
        var required = (totalLoadKg + stepKg) * floorAtFailure;

        bool Earns(int reps) =>
            (TrainingConstants.EpleyRepDivisor + reps + targetRir) * totalLoadKg >= required;

        if (Earns(repRangeMax))
        {
            return repRangeMax;
        }

        var estimate = (int)Math.Ceiling(
            (required / totalLoadKg) - TrainingConstants.EpleyRepDivisor - targetRir);
        var reps = Math.Max(repRangeMax + 1, estimate);

        while (!Earns(reps))
        {
            reps++;
        }

        while (reps - 1 > repRangeMax && Earns(reps - 1))
        {
            reps--;
        }

        return reps;
    }
}
