namespace StrengthPlanner.Domain.Algorithms;

/// <summary>One logged set, as evidence about strength.</summary>
/// <param name="ExerciseId">Exercise the set belongs to; only like is compared with like.</param>
/// <param name="Reps">Reps completed.</param>
/// <param name="Rir">Reps left in reserve.</param>
/// <param name="TotalLoadKg">Load moved, body included — the same total every rule works on.</param>
public sealed record StrengthSample(Guid ExerciseId, int Reps, int Rir, decimal TotalLoadKg);

/// <summary>
/// How much stronger — or weaker — a week was than a comparable earlier one.
///
/// The fatigue score reads a drop in estimated strength as one of its four signals, and it
/// used to measure that as the week's <i>best</i> estimate against the previous week's
/// best, per exercise. Where in the prescribed range the lifter lands is itself part of the
/// prescription, and it moves: a set at the top of an 8-12 week and a set at the floor of
/// the next one are both compliant. Measured through the app's own rules, that pair reads
/// as a <b>9.3% to 9.9% collapse</b> — enough to saturate a signal whose full weight sits
/// at 5% — in weeks where nothing about the plan changed except which end of the range was
/// hit. The audit expected the artefact to come from the rep window moving; with the load
/// re-derived for a new prescription, that part contributes at most 0.42% for hypertrophy
/// and 3.29% for strength.
///
/// So the comparison is made between sets that are actually comparable: the same exercise
/// at the same effective reps, within <see cref="ComparableRepSpread"/>. Estimates rather
/// than raw loads, so that the one rep of tolerance is accounted for rather than ignored;
/// with equal reps the two are the same ratio.
///
/// Null when no pair is comparable. A missing measurement must not read as a decline —
/// that was already the rule for a missing week, and it is the same rule here.
/// </summary>
public static class StrengthChange
{
    /// <summary>
    /// How far apart two sets' effective reps may be and still be compared.
    ///
    /// Zero would be honest but brittle: adjacent weeks of a periodized block overlap
    /// heavily, yet a lifter who does 11 reps one week and 12 the next would produce no
    /// comparison at all. One rep is enough to keep the signal alive across that, and
    /// Epley prices the difference rather than hiding it.
    /// </summary>
    public const int ComparableRepSpread = 1;

    /// <summary>
    /// Relative change in strength between two weeks, averaged over the exercises that
    /// offer a comparable pair; null when none do.
    /// </summary>
    public static decimal? ChangeShare(
        IEnumerable<StrengthSample> current,
        IEnumerable<StrengthSample> previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);

        var calculator = new E1RmCalculator();
        var currentByExercise = Usable(current, calculator);
        var previousByExercise = Usable(previous, calculator);

        var changes = new List<decimal>();

        foreach (var (exerciseId, currentSets) in currentByExercise)
        {
            if (!previousByExercise.TryGetValue(exerciseId, out var previousSets))
            {
                continue;
            }

            var change = BestComparableChange(currentSets, previousSets);

            if (change is not null)
            {
                changes.Add(change.Value);
            }
        }

        return changes.Count == 0 ? null : changes.Average();
    }

    /// <summary>
    /// The change read from the best comparable pair: the earlier week's highest estimate
    /// that has a counterpart at the same effective reps, against this week's best at that
    /// rep count. Measuring against the best the lifter managed then is what the previous
    /// rule did, and it is the conservative direction for detecting a decline.
    /// </summary>
    private static decimal? BestComparableChange(
        IReadOnlyList<Estimate> current,
        IReadOnlyList<Estimate> previous)
    {
        decimal? change = null;
        var bestReference = 0m;

        foreach (var then in previous)
        {
            if (then.OneRepMax <= 0m || then.OneRepMax <= bestReference)
            {
                continue;
            }

            // Na uporedivom broju ponavljanja se uzima najbolja serija tekuce nedelje: pad
            // se meri prema onome sto je vezbac mogao, a ne prema zagrevanju.
            var comparable = current
                .Where(now => Math.Abs(now.EffectiveReps - then.EffectiveReps) <= ComparableRepSpread)
                .Select(now => now.OneRepMax)
                .DefaultIfEmpty(-1m)
                .Max();

            if (comparable < 0m)
            {
                continue;
            }

            bestReference = then.OneRepMax;
            change = (comparable - then.OneRepMax) / then.OneRepMax;
        }

        return change;
    }

    private static Dictionary<Guid, List<Estimate>> Usable(
        IEnumerable<StrengthSample> samples,
        E1RmCalculator calculator)
    {
        var usable = new Dictionary<Guid, List<Estimate>>();

        // Isti predikat koji odlucuje da li serija uopste daje procenu: signal se ne gradi
        // na proceni koju sistem nigde drugde ne priznaje.
        foreach (var sample in samples.Where(sample =>
                     E1RmCalculator.CanEstimateFrom(sample.TotalLoadKg, sample.Reps, sample.Rir)))
        {
            if (!usable.TryGetValue(sample.ExerciseId, out var list))
            {
                list = [];
                usable[sample.ExerciseId] = list;
            }

            list.Add(new Estimate(
                sample.Reps + sample.Rir,
                calculator.EstimateOneRepMax(sample.TotalLoadKg, sample.Reps, sample.Rir)));
        }

        return usable;
    }

    private sealed record Estimate(int EffectiveReps, decimal OneRepMax);
}
