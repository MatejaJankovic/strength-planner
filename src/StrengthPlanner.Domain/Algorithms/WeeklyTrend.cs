namespace StrengthPlanner.Domain.Algorithms;

/// <summary>A muscle a set trained, and how much of the set it is credited with.</summary>
public sealed record TrendMuscle(Guid MuscleGroupId, decimal Contribution);

/// <summary>
/// One logged set, with just what a trend over time needs: when it was lifted, how much, and
/// which block it belongs to. <paramref name="TotalLoadKg"/> is the load including the body mass
/// the movement carries, the same as tonnage everywhere else.
/// </summary>
public sealed record TrendSet(
    DateTime PerformedAt,
    decimal TotalLoadKg,
    int Reps,
    int Rir,
    bool IsFailure,
    Guid BlockId,
    string BlockName,
    string PlanName,
    bool IsDeload,
    IReadOnlyList<TrendMuscle> Muscles);

/// <summary>One calendar week of training, across whichever blocks it touched.</summary>
public sealed record TrendWeek(
    DateTime WeekStart,
    decimal TonnageKg,
    int Sets,
    decimal StimulativeSets,
    IReadOnlyDictionary<Guid, decimal> StimulativeSetsByMuscle,
    Guid? BlockId,
    string? BlockName,
    string? PlanName,
    bool IsDeload,
    bool BlockStarts);

/// <summary>
/// Training over time, across plans and blocks.
///
/// Tonnage and weekly volume used to be read one block at a time, so what happened before the
/// current block, and how one block compared with the one before it, could not be seen. Here the
/// sets are grouped by calendar week instead (Monday to Sunday, UTC), and each week says which
/// block it mostly belonged to, whether it was a deload and whether a new block began in it, so a
/// drop at a block boundary or in a deload is not read as a loss of strength.
/// </summary>
public static class WeeklyTrend
{
    /// <summary>Midnight UTC of the Monday of the week the date falls in.</summary>
    public static DateTime WeekStartOf(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
        var daysFromMonday = ((int)utc.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(utc.Date.AddDays(-daysFromMonday), DateTimeKind.Utc);
    }

    /// <summary>
    /// <paramref name="weeks"/> is how many calendar weeks back from the one that contains
    /// <paramref name="now"/>, that week included; null reaches back to the first logged set.
    ///
    /// Weeks with no sets are kept once training has begun: a gap, or a deload with no training, is
    /// information. The weeks before the first set in the range are not: they say nothing, and a
    /// range wider than the history would otherwise open with a screenful of "no training".
    /// </summary>
    public static IReadOnlyList<TrendWeek> Build(IReadOnlyList<TrendSet> sets, DateTime now, int? weeks)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var lastWeek = WeekStartOf(now);

        // A set from a week that has not begun yet (a clock that ran ahead) is not training that
        // has happened.
        var counted = sets
            .Where(set => WeekStartOf(set.PerformedAt) <= lastWeek)
            .ToList();

        if (weeks is not null)
        {
            var rangeStart = lastWeek.AddDays(-7 * (Math.Max(1, weeks.Value) - 1));
            counted = counted.Where(set => WeekStartOf(set.PerformedAt) >= rangeStart).ToList();
        }

        if (counted.Count == 0)
        {
            return [];
        }

        var byWeek = counted
            .GroupBy(set => WeekStartOf(set.PerformedAt))
            .ToDictionary(group => group.Key, group => group.ToList());

        // The first week with a set, not the start of the range: the weeks before it say nothing.
        var firstWeek = byWeek.Keys.Min();

        var result = new List<TrendWeek>();
        Guid? previousBlock = null;

        for (var weekStart = firstWeek; weekStart <= lastWeek; weekStart = weekStart.AddDays(7))
        {
            if (!byWeek.TryGetValue(weekStart, out var weekSets))
            {
                result.Add(new TrendWeek(weekStart, 0m, 0, 0m, new Dictionary<Guid, decimal>(), null, null, null, false, false));
                continue;
            }

            // The block with the most sets in the week; on a tie the one trained later, because a
            // week that straddles a boundary is mostly the new block when the new block started
            // mid-week and the old one ended earlier.
            var block = weekSets
                .GroupBy(set => set.BlockId)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Max(set => set.PerformedAt))
                .First();
            var blockSet = block.First();

            var byMuscle = new Dictionary<Guid, decimal>();
            foreach (var set in weekSets)
            {
                var credit = StimulativeVolume.CreditFor(set.Rir, set.IsFailure);
                foreach (var muscle in set.Muscles)
                {
                    byMuscle[muscle.MuscleGroupId] = byMuscle.GetValueOrDefault(muscle.MuscleGroupId) + muscle.Contribution * credit;
                }
            }

            result.Add(new TrendWeek(
                weekStart,
                weekSets.Sum(set => set.TotalLoadKg * set.Reps),
                weekSets.Count,
                weekSets.Sum(set => StimulativeVolume.CreditFor(set.Rir, set.IsFailure)),
                byMuscle,
                block.Key,
                blockSet.BlockName,
                blockSet.PlanName,
                weekSets.Count(set => set.IsDeload) * 2 >= weekSets.Count,
                previousBlock != block.Key));

            previousBlock = block.Key;
        }

        return result;
    }
}
