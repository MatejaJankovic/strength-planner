namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// How much a completed set actually counts as training volume.
///
/// Counting every logged set equally treats a set stopped five reps short of failure the
/// same as one stopped at the last rep. The handbook is explicit that it is not the same
/// thing: <i>"Jedini volumen koji se računa je volumen koji sadrži mehaničku tenziju,
/// odnosno serije koje su odrađene do ili blizu otkaza"</i>, and it puts the outer bound
/// at <i>"minimalno RIR 4"</i>. Mechanical tension — the actual driver of hypertrophy —
/// appears only on the last few reps before failure, so a set that never gets near
/// failure produces fatigue without producing the stimulus the volume number is meant to
/// stand for.
///
/// Without this weighting a lifter who does twenty easy sets is told they are over their
/// MRV and should cut back, when by the handbook they have not done a single stimulative
/// set.
///
/// The cut-off is the handbook's, and it is more conservative than the evidence. "Only the
/// last few reps carry tension" is the effective-reps hypothesis, not a measured mechanism:
/// the meta-regressions of Robinson et al. (2024) have hypertrophy falling off gradually with
/// estimated reserve rather than at a step, and with volume equated, sets to failure and sets
/// close to it grow muscle similarly (Refalo et al. 2023) - though those non-failure sets were
/// mostly near failure, so they say little about a set at RIR 4 or 5. The step is kept on
/// purpose (decision D1: the handbook is the default unless the literature is clearly on the
/// other side, and here it is only smoother). Training weeks never prescribe more than RIR 3,
/// so a set done to plan there always counts in full; a strength block's deload prescribes
/// RIR 4, so there a set done to plan counts half.
/// </summary>
public static class StimulativeVolume
{
    /// <summary>Furthest from failure a set can be and still count in full.</summary>
    public const int FullCreditRir = 3;

    /// <summary>Last RIR that counts at all; beyond this a set is fatigue, not volume.</summary>
    public const int PartialCreditRir = 4;

    /// <summary>Share of a set that counts at <see cref="PartialCreditRir"/>.</summary>
    public const decimal PartialCredit = 0.5m;

    /// <summary>
    /// Fraction of a set that counts as volume, from its distance to failure.
    /// A set taken to failure is always full credit — it cannot be further from failure
    /// than failure itself, whatever RIR was recorded alongside it.
    /// </summary>
    public static decimal CreditFor(int rir, bool isFailure)
    {
        if (isFailure || rir <= FullCreditRir)
        {
            return 1m;
        }

        return rir == PartialCreditRir ? PartialCredit : 0m;
    }

    /// <summary>
    /// Credit for a working set, taking the same view of failure as the progression engine.
    /// </summary>
    public static decimal CreditFor(WorkingSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return CreditFor(set.Rir, set.IsFailure);
    }
}
