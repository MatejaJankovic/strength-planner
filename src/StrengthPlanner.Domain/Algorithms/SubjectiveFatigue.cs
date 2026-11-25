namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// The lifter's own answer to "how tired are you after this workout", 1 to 5, and how a week of
/// answers becomes a signal for the fatigue score.
///
/// <b>The level of the answer is not read, its rise is.</b> People use a five-point scale
/// differently: one lifter's 3 is another's 4. What carries information is a week that feels worse than
/// the lifter's own earlier weeks of the same block, which is how subjective fatigue is followed in
/// practice (a change against one's own baseline). The first week has no baseline and says nothing.
///
/// <b>A single week's rise is not enough either.</b> A bad day lowers the week's strength and the reserve the
/// lifter reports, and it raises the answer too: three signals from one cause. The same argument that
/// made the strength term wait for confirmation (<see cref="FatigueEvaluator.ConfirmedStrengthDrop"/>)
/// applies here, so the rise counts only when the previous week rose as well
/// (<see cref="FatigueEvaluator.ConfirmedSubjectiveRise"/>). Simulated with the noise model of the other
/// signals (<c>docs/simulations/subjective_fatigue.py</c>), whose parameters for the answer are
/// assumptions and not measurements: without the confirmation a lifter who truly progressed was deloaded in 20-44%
/// of six-week blocks at MRV; with it in 5-6%, while a real 3% weekly decline was caught in 69-72% of blocks
/// instead of 55%.
/// </summary>
public static class SubjectiveFatigue
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    /// <summary>A rise of this many points over the lifter's own baseline gives the signal its full weight.</summary>
    public const decimal RiseAtFullWeight = 1.0m;

    /// <summary>
    /// The previous week's rise at which it confirms this one. Half the rise that is worth a full weight: a
    /// week that did not clearly rise is not a second week of the same thing.
    /// </summary>
    public const decimal RiseToConfirm = 0.5m;

    public static bool IsValid(int rating) => rating is >= MinRating and <= MaxRating;

    /// <summary>The week's answers as one number, or null when the lifter gave none.</summary>
    public static decimal? WeekMean(IEnumerable<int> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var list = ratings.ToList();

        return list.Count == 0 ? null : (decimal)list.Sum() / list.Count;
    }

    /// <summary>
    /// The rise of a week and the rise of the training week before it, from the means of the weeks that
    /// have answers.
    ///
    /// The week before is the previous <b>training</b> week of the block, answered or not: a week without answers
    /// is not a rise, so it cannot confirm one. Deload weeks are not in <paramref name="trainingWeeks"/>, because
    /// a lifter feels better in them and that would pull the baseline down.
    /// </summary>
    /// <param name="weekMeans">Mean answer by week number, only for weeks with at least one answer.</param>
    /// <param name="trainingWeeks">Numbers of all training (non-deload) weeks of the block.</param>
    /// <param name="weekNumber">The week being scored.</param>
    public static (decimal Rise, decimal PreviousRise) Rises(
        IReadOnlyDictionary<int, decimal> weekMeans,
        IReadOnlyCollection<int> trainingWeeks,
        int weekNumber)
    {
        ArgumentNullException.ThrowIfNull(weekMeans);
        ArgumentNullException.ThrowIfNull(trainingWeeks);

        var earlier = trainingWeeks.Where(week => week < weekNumber).Order().ToList();

        decimal? MeanOf(int week) => weekMeans.TryGetValue(week, out var mean) ? mean : null;
        List<decimal> MeansOf(IEnumerable<int> weeks) =>
            weeks.Select(MeanOf).Where(mean => mean is not null).Select(mean => mean!.Value).ToList();

        var rise = Rise(MeanOf(weekNumber), MeansOf(earlier));

        if (earlier.Count == 0)
        {
            return (rise, 0m);
        }

        var previousWeek = earlier[^1];
        var previousRise = Rise(MeanOf(previousWeek), MeansOf(earlier.Take(earlier.Count - 1)));

        return (rise, previousRise);
    }

    /// <summary>
    /// How much a week's mean answer rose above the mean of the weeks before it. Zero when either is missing:
    /// no answer is no evidence, and it must not read as a flat week or as a rise.
    /// </summary>
    /// <param name="weekMean">The week's own mean, or null when it has no answers.</param>
    /// <param name="earlierWeekMeans">The means of the earlier weeks of the block that have answers.</param>
    public static decimal Rise(decimal? weekMean, IReadOnlyCollection<decimal> earlierWeekMeans)
    {
        ArgumentNullException.ThrowIfNull(earlierWeekMeans);

        if (weekMean is null || earlierWeekMeans.Count == 0)
        {
            return 0m;
        }

        return weekMean.Value - earlierWeekMeans.Average();
    }
}
