namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// One set as it was logged, with the load it was performed at.
/// </summary>
/// <param name="WeightKg">Load added on the bar, belt or stack, in kilograms.</param>
/// <param name="Reps">Reps completed.</param>
/// <param name="Rir">Reps left in reserve as judged by the lifter.</param>
/// <param name="IsFailure">Whether the set was taken to failure.</param>
/// <param name="BodyweightLoadKg">
/// Body mass the movement carried, snapshotted when the set was logged; zero for everything
/// loaded externally. Kept beside the added load rather than folded into it, because the
/// lifter logs and reads the added number while the training rules need the total.
/// </param>
public sealed record LoggedSet(
    decimal WeightKg,
    int Reps,
    int Rir,
    bool IsFailure = false,
    decimal BodyweightLoadKg = 0m)
{
    /// <summary>Everything the set moved: what was added plus the body it lifted.</summary>
    public decimal TotalLoadKg => WeightKg + BodyweightLoadKg;

    /// <summary>The same set as the progression engine sees it, without its load.</summary>
    public WorkingSet ToWorkingSet()
    {
        return new WorkingSet(Reps, Rir, IsFailure);
    }
}

/// <summary>
/// Which load a session's progression is judged against, and which of its sets speak for it.
///
/// Progression used to start from the <b>average</b> weight of every logged set. A session of
/// 100, 100 and 80 kg therefore progressed from 93.33 kg, so a back-off set pulled the next
/// week down below a weight the lifter had just handled twice. The reference is the heaviest
/// load actually lifted.
///
/// Lighter sets are not simply discarded. One that ended with nothing in reserve is evidence
/// about the heavier load too: failing at 90 kg means failing at least as early at 100 kg, so
/// counting it as a set at the reference weight can only understate the correction. A lighter
/// set that kept reserve says nothing about the reference load — its RIR was measured against
/// a different weight — so it is left out.
/// </summary>
/// <param name="ReferenceWeightKg">Heaviest load added in the session.</param>
/// <param name="ReferenceBodyweightLoadKg">
/// Body mass the reference set carried, as it was snapshotted then.
/// </param>
/// <param name="WorkingSets">Sets that speak for that load, in the order they were logged.</param>
/// <param name="ExcludedLighterSets">How many lighter sets kept reserve and were left out.</param>
public sealed record WorkingLoad(
    decimal ReferenceWeightKg,
    decimal ReferenceBodyweightLoadKg,
    IReadOnlyList<WorkingSet> WorkingSets,
    int ExcludedLighterSets)
{
    /// <summary>Everything the reference set moved.</summary>
    public decimal ReferenceTotalLoadKg => ReferenceWeightKg + ReferenceBodyweightLoadKg;

    /// <summary>
    /// Picks the reference load and its sets, or null when nothing was logged.
    /// </summary>
    public static WorkingLoad? Select(IReadOnlyList<LoggedSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        if (sets.Count == 0)
        {
            return null;
        }

        // Poređenje ide po UKUPNOM opterećenju: kod vežbi sa telesnom masom je razlika
        // između dve serije sa 0 i 5 dodatnih kilograma sitna naspram tela koje obe nose.
        var referenceSet = sets.MaxBy(set => set.TotalLoadKg)!;
        var reference = referenceSet.TotalLoadKg;
        var working = new List<WorkingSet>(sets.Count);
        var excluded = 0;

        foreach (var set in sets)
        {
            if (set.TotalLoadKg == reference || set.Rir == 0 || set.IsFailure)
            {
                working.Add(set.ToWorkingSet());
            }
            else
            {
                excluded++;
            }
        }

        return new WorkingLoad(
            referenceSet.WeightKg,
            referenceSet.BodyweightLoadKg,
            working,
            excluded);
    }
}
