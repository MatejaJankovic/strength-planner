namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Whether one load step fits the capacity a session showed, and how many reps it takes to
/// earn the step when the step is large for the load.
///
/// Double progression adds one step once every set reaches the top of the range, and the
/// next session starts again at the bottom. That reset pays for the step, and by Epley it
/// pays for a lot: a lifter at the top of 8-12 with one rep in reserve can take about 13%
/// more load and still complete 8 reps. Every barbell step on a real load fits. A 2 kg
/// dumbbell step on an 8 kg lateral raise is 25%, and it did not fit: the lifter who did
/// 3 x 12 at 8 kg was handed 10 kg, where Epley leaves about three reps at RIR 1 - five
/// below the floor of the range the session prescribes. Measured on the engine as it was
/// before this class existed.
///
/// The measure is the floor of the range after the step, reachable <b>at all</b> - even at
/// failure. Requiring it at the target RIR was the first idea and is too strict: a narrow
/// week (11-12 at RIR 2) absorbs only about 2% at the target RIR, so even 100 kg on a
/// 2.5 kg bar would have stopped stepping, and a fixed 5 x 5 absorbs nothing at all.
///
/// Two regimes, and the line between them is <see cref="FitsAtTarget"/>:
///
/// <list type="bullet">
/// <item>Where a lifter at the top of the range with the target reserve could take the
/// step, nothing changes: the step rule of rounds 9 and 10 decides, including the narrow
/// week that holds after a set to failure.</item>
/// <item>Where they could not, the step is given only when every set's own capacity - reps
/// plus the reserve actually left - absorbs it (<see cref="Absorbs"/>). A first version of
/// this change counted the reps above the range twice instead, once to absorb the step and
/// once more to pay for a missing reserve, and stepped a 30 kg bar in an 11-12 week to
/// 32.5 kg after sets of 13 to failure, where Epley leaves 9.7 reps. Found in review.</item>
/// </list>
///
/// The screen needs a number to aim at, and that is <see cref="RepsToEarnStep"/>: the reps
/// that absorb the step at the target reserve. A narrow or fixed prescription gets no such
/// number - 5 x 5 is a program, not a range to be stretched to six - and there the reserve
/// alone carries the step.
/// </summary>
public static class StepAbsorption
{
    /// <summary>
    /// Whether a set of <paramref name="reps"/> with <paramref name="rir"/> left in reserve,
    /// at <paramref name="totalLoadKg"/>, leaves the floor of the range reachable after one
    /// step more: (30 + reps + rir) * load &gt;= (load + step) * (30 + min).
    ///
    /// Compared by cross-multiplication, without dividing: (load + step) / load need not be
    /// a finite decimal, and rounding it could turn an exact tie into a missing rep.
    /// </summary>
    public static bool Absorbs(decimal totalLoadKg, decimal stepKg, int repRangeMin, int reps, int rir)
    {
        if (totalLoadKg <= 0 || stepKg <= 0)
        {
            return true;
        }

        return (TrainingConstants.EpleyRepDivisor + reps + rir) * totalLoadKg
               >= (totalLoadKg + stepKg) * (TrainingConstants.EpleyRepDivisor + repRangeMin);
    }

    /// <summary>
    /// Whether the step fits the prescription as written: a lifter at the top of the range
    /// with exactly the target reserve could take it. Where it does, the step rule is the
    /// one progression always had.
    /// </summary>
    public static bool FitsAtTarget(
        decimal totalLoadKg,
        decimal stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir)
    {
        return Absorbs(totalLoadKg, stepKg, repRangeMin, repRangeMax, targetRir);
    }

    /// <summary>
    /// A range narrower than its target reserve (3-4 at RIR 3, 11-12 at RIR 2, a fixed 5 at
    /// RIR 2). The reps are the prescription there, and the reserve is what moves.
    /// </summary>
    public static bool IsNarrow(int repRangeMin, int repRangeMax, int targetRir)
    {
        return targetRir > repRangeMax - repRangeMin;
    }

    /// <summary>
    /// The rep count to aim at before the step comes: <paramref name="repRangeMax"/> when the
    /// step fits the prescription or the range is narrow, otherwise the smallest rep count
    /// whose set, at the target reserve, absorbs the step (17 for 8 kg on a 2 kg step in
    /// 8-12 at RIR 1). A guide for the screen; progression itself judges the sets that were
    /// actually done, with the reserve they actually left.
    /// </summary>
    /// <param name="totalLoadKg">Load being lifted, body portion included.</param>
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
        if (IsNarrow(repRangeMin, repRangeMax, targetRir)
            || FitsAtTarget(totalLoadKg, stepKg, repRangeMin, repRangeMax, targetRir))
        {
            return repRangeMax;
        }

        var required = (totalLoadKg + stepKg) * (TrainingConstants.EpleyRepDivisor + repRangeMin);
        var estimate = (int)Math.Ceiling(
            (required / totalLoadKg) - TrainingConstants.EpleyRepDivisor - targetRir);
        var reps = Math.Max(repRangeMax + 1, estimate);

        while (!Absorbs(totalLoadKg, stepKg, repRangeMin, reps, targetRir))
        {
            reps++;
        }

        while (reps - 1 > repRangeMax && Absorbs(totalLoadKg, stepKg, repRangeMin, reps - 1, targetRir))
        {
            reps--;
        }

        return reps;
    }
}
