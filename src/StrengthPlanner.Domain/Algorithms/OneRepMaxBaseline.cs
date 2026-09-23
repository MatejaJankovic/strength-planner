using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>One stored one-rep max value, as the baseline rule sees it.</summary>
/// <param name="ValueKg">The value in kilograms.</param>
/// <param name="Source">Whether the lifter entered it or the system estimated it.</param>
/// <param name="RecordedAt">When it was written.</param>
public sealed record OneRepMaxSample(decimal ValueKg, OneRepMaxSource Source, DateTime RecordedAt);

/// <summary>
/// Which stored one-rep max the planner should start from.
///
/// The rule used to be "the best value in the last 56 days", on the grounds that the newest
/// record may come from a weak day. That holds as long as every record is trustworthy — but
/// a single optimistic RIR produces an estimate several percent above the rest, and "best"
/// then keeps that one number for eight weeks: it becomes the starting load of the next
/// block and the baseline for every periodized recompute.
///
/// Two corrections, both of them about a single record being able to speak for the window:
///
/// <list type="bullet">
/// <item>An <b>estimated</b> best standing more than
/// <see cref="TrainingConstants.OneRepMaxOutlierTolerance"/> above the next best is treated
/// as an outlier and the next best is used — but only once the window holds at least
/// <see cref="TrainingConstants.OneRepMaxOutlierMinSamples"/> values, because with two there
/// is nothing to corroborate and the rule would just take the lower one. A value the lifter
/// typed is never demoted: it is a statement, not a measurement.</item>
/// <item>A manually entered value supersedes everything older than it. It is a deliberate
/// statement about today, and it is the only way for the lifter to correct an inflated
/// estimate downward — under "best in the window" a lower manual value was simply
/// ignored.</item>
/// <item>Non-positive values are ignored entirely. Bodyweight exercises were logged at 0 kg
/// and every completed session wrote a 0 kg estimate, so such records exist in history; a
/// window holding one would otherwise hand the planner a target of 0 kg.</item>
/// </list>
/// </summary>
public static class OneRepMaxBaseline
{
    /// <summary>
    /// The value to plan from, or null when the window holds nothing usable.
    /// </summary>
    /// <param name="samples">Stored values for one exercise, in any order.</param>
    /// <param name="now">Reference point for the window; the domain never reads the clock.</param>
    /// <param name="lookbackDays">Width of the window in days.</param>
    /// <param name="allowStaleFallback">
    /// When the window is empty: true falls back to the newest sample ever (what generating a
    /// block needs, so a lifter returning after a pause keeps their loads), false returns
    /// null (what a mid-block recompute needs, where a months-old record is not evidence).
    /// </param>
    public static decimal? Select(
        IReadOnlyList<OneRepMaxSample> samples,
        DateTime now,
        int lookbackDays,
        bool allowStaleFallback)
    {
        ArgumentNullException.ThrowIfNull(samples);

        // Zapis od 0 kg nije procena nego trag vežbe bez opterećenja; da je ovde, plan bi
        // dobio cilj od 0 kg.
        var usable = samples.Where(sample => sample.ValueKg > 0).ToList();

        if (usable.Count == 0)
        {
            return null;
        }

        var cutoff = now.AddDays(-lookbackDays);
        var inWindow = usable.Where(sample => sample.RecordedAt >= cutoff).ToList();

        if (inWindow.Count == 0)
        {
            return allowStaleFallback
                ? usable.MaxBy(sample => sample.RecordedAt)!.ValueKg
                : null;
        }

        var newestManual = inWindow
            .Where(sample => sample.Source == OneRepMaxSource.Manual)
            .Select(sample => (DateTime?)sample.RecordedAt)
            .DefaultIfEmpty(null)
            .Max();

        var considered = newestManual is null
            ? inWindow
            : inWindow.Where(sample => sample.RecordedAt >= newestManual.Value).ToList();

        var ordered = considered
            .OrderByDescending(sample => sample.ValueKg)
            .ToList();

        var best = ordered[0];

        // Ručni unos se ne obara: vežbač je rekao koliko diže, a ne sistem procenio.
        if (best.Source == OneRepMaxSource.Manual
            || ordered.Count < TrainingConstants.OneRepMaxOutlierMinSamples)
        {
            return best.ValueKg;
        }

        var secondBest = ordered[1].ValueKg;

        return best.ValueKg > secondBest * (1 + TrainingConstants.OneRepMaxOutlierTolerance)
            ? secondBest
            : best.ValueKg;
    }

    /// <summary>
    /// The sample the rule picks, rather than only its value, for screens that show the
    /// stored record itself.
    /// </summary>
    public static OneRepMaxSample? SelectSample(
        IReadOnlyList<OneRepMaxSample> samples,
        DateTime now,
        int lookbackDays,
        bool allowStaleFallback)
    {
        var value = Select(samples, now, lookbackDays, allowStaleFallback);

        if (value is null)
        {
            return null;
        }

        // Ista vrednost može da stoji u više zapisa; prikazuje se najsvežiji, jer je to
        // datum koji korisnik vidi kao "kada".
        return samples
            .Where(sample => sample.ValueKg == value.Value)
            .OrderByDescending(sample => sample.RecordedAt)
            .First();
    }
}
