namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// One completed working set as the progression engine sees it.
/// </summary>
/// <param name="Reps">Reps actually completed.</param>
/// <param name="Rir">Reps left in reserve as judged by the lifter; always 0 for a failed set.</param>
/// <param name="IsFailure">
/// True when the set was explicitly marked as taken to failure. Not the only source of
/// truth for that fact — see <see cref="EffectiveRir"/>.
/// </param>
public sealed record WorkingSet(int Reps, int Rir, bool IsFailure = false)
{
    /// <summary>
    /// RIR used for auto-regulation, extended below zero for a set that failed short of
    /// the range.
    ///
    /// Failure is detected from the numbers themselves, not only from
    /// <see cref="IsFailure"/>. Zero reps in reserve below the range floor <b>is</b>
    /// failure by definition — "I had nothing left" and "I could not do another rep" are
    /// the same physical event whether or not a checkbox recorded it. Reported from real
    /// use: a set logged as 6 reps, RIR 0, out of an 8-12 range, without the checkbox,
    /// scored the same mild -3% as a completed set at the bottom of the range — because
    /// the earlier version of this method looked at <see cref="IsFailure"/> alone. The
    /// checkbox is easy to forget precisely when it matters most: mid-failure, not before.
    ///
    /// RIR above zero is left untouched even below the range floor, since that is a
    /// genuine, different signal — the lifter stopped on purpose with reserve left
    /// (pain, time, form), not because they ran out of reps.
    ///
    /// The RIR scale otherwise stops at 0, so a set that failed short of the target rep
    /// range would report the same 0 as a set that just barely reached the bottom of the
    /// range. That is what made downward correction so much narrower than upward
    /// correction: at a target RIR of 1 the worst possible signal was -1 point, or -3%
    /// per session. Counting reps missed against the bottom of the range as negative RIR
    /// makes the signal symmetric — failing five reps short reads as -5 and reaches the
    /// same 10% cap that an easy session reaches upward.
    /// </summary>
    public int EffectiveRir(int repRangeMin)
    {
        if (!ImpliesFailure(Reps, Rir, repRangeMin, IsFailure))
        {
            return Rir;
        }

        return -Math.Max(0, repRangeMin - Reps);
    }

    /// <summary>
    /// Whether the numbers themselves describe a failure, regardless of the explicit
    /// checkbox.
    ///
    /// Exists as a public, named method instead of being folded into <see cref="EffectiveRir"/>
    /// because the infrastructure layer needs the same definition of "what counts as
    /// failure": when a set is written, the stored <c>IsFailure</c> has to agree with the
    /// correction that is actually applied, or training history claims "RIR 0" for
    /// something that was computed as a failure. If that rule existed in two places — here
    /// and in the service that writes to the database — they would diverge the first time
    /// someone changed one and forgot the other.
    /// </summary>
    public static bool ImpliesFailure(int reps, int rir, int repRangeMin, bool explicitlyMarked)
    {
        return explicitlyMarked || (rir == 0 && reps < repRangeMin);
    }
}
