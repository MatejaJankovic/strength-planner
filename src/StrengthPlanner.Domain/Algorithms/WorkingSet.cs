namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// One completed working set as the progression engine sees it.
/// </summary>
/// <param name="Reps">Reps actually completed.</param>
/// <param name="Rir">Reps left in reserve as judged by the lifter; always 0 for a failed set.</param>
/// <param name="IsFailure">
/// True when the set was taken to failure — the lifter could not complete another rep.
/// </param>
public sealed record WorkingSet(int Reps, int Rir, bool IsFailure = false)
{
    /// <summary>
    /// RIR used for auto-regulation, extended below zero for failed sets.
    ///
    /// The RIR scale stops at 0, so a set that failed short of the target rep range
    /// reports the same 0 as a set that just barely reached the bottom of the range.
    /// That is what made downward correction so much narrower than upward correction:
    /// at a target RIR of 1 the worst possible signal was -1 point, or -3% per session.
    /// Counting reps missed against the bottom of the range as negative RIR makes the
    /// signal symmetric — failing five reps short reads as -5 and reaches the same
    /// 10% cap that an easy session reaches upward.
    /// </summary>
    public int EffectiveRir(int repRangeMin)
    {
        if (!IsFailure)
        {
            return Rir;
        }

        return -Math.Max(0, repRangeMin - Reps);
    }
}
