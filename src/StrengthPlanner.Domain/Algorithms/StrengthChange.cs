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
/// best, per exercise, with no test of whether the two sets were comparable at all. Where
/// in the prescribed range the lifter lands is itself part of the prescription: a set at
/// the top of an 8-12 week and a set at the floor of the next are both compliant, and the
/// estimate from 12 reps is far above the one from 8 at a similar load.
///
/// Measured in the running app, a week logged at the top followed by one logged at the
/// floor — nothing but compliance in both:
///
/// <list type="bullet">
/// <item><b>Flat block</b> (the default, where the same prescription carries the load
/// forward with a step): the main lifts read 154.1 against 143.0, a 7.2% drop, the average
/// over the week's exercises is 3.5%, and the fatigue score picks up <b>0.174</b> from a
/// week in which nothing went wrong.</item>
/// <item><b>Periodized block</b>: <b>nothing</b>. A changed prescription re-derives its
/// load from the week's own fresh estimate (105 kg became 117.5 kg, not 110), so the next
/// reading lands within 0.8% and the score is identical either way.</item>
/// </list>
///
/// Which reverses the audit's expectation: it blamed the rep window moving, and the moving
/// window is the case that corrects itself. (An earlier version of this comment quoted
/// 9.3-9.9%, computed by holding the one-rep max fixed across the block. No path in the app
/// does that - the estimate feeds the next load - so the figure described a lifter this
/// system does not have.)
///
/// So the comparison is made between sets that are actually comparable: the same exercise
/// at the same effective reps, within <see cref="ComparableRepSpread"/>. Estimates rather
/// than raw loads, so that the one rep of tolerance is accounted for rather than ignored;
/// with equal reps the two are the same ratio.
///
/// Null when no pair is comparable. A missing measurement must not read as a decline —
/// that was already the rule for a missing week, and it is the same rule here.
///
/// A set above <see cref="TrainingConstants.EpleyRepCap"/> gives no estimate, and since
/// isolations are prescribed 10-20 most of their sets are such sets. Without a second
/// reading the side delts and the calves, which only isolations train, would have stopped
/// telling the volume limits anything. So when an exercise offers no pair of estimates, its
/// sets are compared at the <b>same load</b> (<see cref="SameLoadChange"/>): there, more
/// effective reps is more strength, and no maximum has to be read to say so.
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

        var currentSamples = current.ToList();
        var previousSamples = previous.ToList();

        var calculator = new E1RmCalculator();
        var currentByExercise = Usable(currentSamples, calculator);
        var previousByExercise = Usable(previousSamples, calculator);
        var currentAtLoad = NearFailure(currentSamples);
        var previousAtLoad = NearFailure(previousSamples);

        var changes = new List<decimal>();

        foreach (var exerciseId in currentAtLoad.Keys.Where(previousAtLoad.ContainsKey))
        {
            var change = currentByExercise.TryGetValue(exerciseId, out var currentSets)
                         && previousByExercise.TryGetValue(exerciseId, out var previousSets)
                ? BestComparableChange(currentSets, previousSets)
                : null;

            change ??= SameLoadChange(currentAtLoad[exerciseId], previousAtLoad[exerciseId]);

            if (change is not null)
            {
                changes.Add(change.Value);
            }
        }

        return changes.Count == 0 ? null : changes.Average();
    }

    /// <summary>
    /// The change read at one load, for sets the estimate cannot read: the heaviest load of
    /// the earlier week that the lifter used again, and at it the most effective reps now
    /// against the most then, priced on the Epley curve, (30 + now) / (30 + then) - 1. At
    /// equal load that is exactly the ratio two estimates would give, so the two readings
    /// agree wherever both exist.
    ///
    /// At least one of the two sets must lie above the Epley cap. Two sets at or below it
    /// are the estimate's to compare, and where it declined - effective reps too far apart -
    /// this must not pair them behind its back: that decision is round 11's, and it stands
    /// for the compounds exactly as it was.
    ///
    /// A load that was not used again gives nothing, which is the common case the week after
    /// a step: the reps fall because the load rose. That is the rule for silence, not a
    /// decline.
    /// </summary>
    private static decimal? SameLoadChange(
        IReadOnlyList<StrengthSample> current,
        IReadOnlyList<StrengthSample> previous)
    {
        foreach (var load in previous.Select(sample => sample.TotalLoadKg).Distinct().OrderDescending())
        {
            var then = previous.Where(sample => sample.TotalLoadKg == load).ToList();
            var now = current.Where(sample => sample.TotalLoadKg == load).ToList();

            if (now.Count == 0)
            {
                continue;
            }

            var bestThen = then.MaxBy(sample => sample.Reps + sample.Rir)!;
            var bestNow = now.MaxBy(sample => sample.Reps + sample.Rir)!;

            if (bestThen.Reps <= TrainingConstants.EpleyRepCap && bestNow.Reps <= TrainingConstants.EpleyRepCap)
            {
                continue;
            }

            return (TrainingConstants.EpleyRepDivisor + bestNow.Reps + bestNow.Rir)
                   / (TrainingConstants.EpleyRepDivisor + bestThen.Reps + bestThen.Rir)
                   - 1m;
        }

        return null;
    }

    /// <summary>
    /// Sets that are evidence of strength at all - a load, and no further from failure than
    /// an estimate would accept - grouped by exercise, whatever their rep count.
    /// </summary>
    private static Dictionary<Guid, List<StrengthSample>> NearFailure(IEnumerable<StrengthSample> samples)
    {
        return samples
            .Where(sample => sample.TotalLoadKg > 0
                             && sample.Reps > 0
                             && sample.Rir >= 0
                             && sample.Rir <= TrainingConstants.E1RmMaxRir)
            .GroupBy(sample => sample.ExerciseId)
            .ToDictionary(group => group.Key, group => group.ToList());
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
