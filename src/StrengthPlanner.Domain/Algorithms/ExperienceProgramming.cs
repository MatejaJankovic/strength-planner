using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Turns the lifter's experience level into the programming decisions the handbook ties
/// to it.
///
/// The handbook devotes a whole table to the three levels, and its closing line is the
/// point: <i>"napredni vežbač bi pregoreo od treninga početnika"</i>. The same plan is
/// not merely suboptimal for a different level — it is the wrong plan.
///
/// Four things follow from the level:
///
/// <list type="bullet">
/// <item>how many exercises a session holds, and how they split between compound and
/// isolation — <i>"2-3 složene vežbe po treningu"</i> for a beginner against
/// <i>"do 3 složene vežbe nedeljno, pretežno izolacije"</i> for an advanced lifter;</item>
/// <item>sets per exercise — beginners run <i>"srednji volumen, ali visok intenzitet"</i>,
/// intermediates <i>"veći volumen"</i>, advanced <i>"manji volumen, uz napredne
/// tehnike"</i>;</item>
/// <item>where the volume landmarks start, because a beginner's set is a weaker stimulus
/// and their recovery is less developed;</item>
/// <item>whether fatigue should pull a deload forward at all — the handbook is blunt that
/// <i>"početnici ne treba da razmišljaju o ovome"</i>.</item>
/// </list>
/// </summary>
public static class ExperienceProgramming
{
    /// <summary>Working sets per exercise the plan starts with.</summary>
    public static int StartingSetsPerExercise(ExperienceLevel level) => level switch
    {
        // Srednji volumen uz visok intenzitet: napredak je još linearan i dolazi
        // od učenja pokreta, ne od gomilanja serija.
        ExperienceLevel.Beginner => 3,

        // Veći volumen — ovde je volumen glavna poluga napretka.
        ExperienceLevel.Intermediate => 4,

        // Manji volumen po vežbi, ali težih i preciznije biranih serija.
        ExperienceLevel.Advanced => 3,

        _ => 3
    };

    /// <summary>
    /// How many exercises a single session holds. An advanced lifter trains fewer things
    /// per session but harder; a beginner needs room for the basic patterns.
    /// </summary>
    public static int ExercisesPerSession(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => 5,
        ExperienceLevel.Intermediate => 6,
        ExperienceLevel.Advanced => 6,
        _ => 6
    };

    /// <summary>
    /// Most compound exercises allowed in one session. This is the handbook's central
    /// distinction: compounds carry the largest stimulus but also the most fatigue, so
    /// the lifter who recovers best from them is the one who needs them most.
    /// </summary>
    public static int MaxCompoundsPerSession(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => 3,
        ExperienceLevel.Intermediate => 2,
        ExperienceLevel.Advanced => 1,
        _ => 2
    };

    /// <summary>
    /// Multiplier applied to the seeded MEV/MAV/MRV values.
    ///
    /// A beginner's set is a weaker stimulus — they cannot yet recruit the largest motor
    /// units — and their recovery capacity is undeveloped, so the whole band sits lower.
    /// An advanced lifter tolerates and needs more. Personal adaptation then moves from
    /// this starting point rather than from the population average.
    /// </summary>
    public static decimal LandmarkScale(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => 0.8m,
        ExperienceLevel.Intermediate => 1.0m,
        ExperienceLevel.Advanced => 1.2m,
        _ => 1.0m
    };

    /// <summary>
    /// Fatigue score at or above which the next week becomes a deload, or null when
    /// fatigue should not pull a deload forward at all.
    ///
    /// Beginners get null on purpose. Their RIR estimates are unreliable — the handbook
    /// says they stop at the burn believing they are at failure — so the very signals the
    /// score is built from are noisiest exactly where an unnecessary deload costs the most
    /// progress. They keep the planned end-of-block deload; they simply do not get an
    /// early one.
    /// </summary>
    public static decimal? DeloadThreshold(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => null,
        ExperienceLevel.Intermediate => 0.60m,
        ExperienceLevel.Advanced => 0.50m,
        _ => 0.60m
    };

    /// <summary>Scales a seeded landmark, never below one set.</summary>
    public static int ScaleLandmark(int seeded, ExperienceLevel level)
    {
        var scaled = (int)Math.Round(seeded * LandmarkScale(level), MidpointRounding.AwayFromZero);

        return Math.Max(1, scaled);
    }

    /// <summary>
    /// Scales a whole landmark triple, keeping MEV &lt; MAV &lt; MRV. Rounding can collapse
    /// a narrow band, so the order is restored rather than trusted.
    /// </summary>
    public static VolumeLandmarkValues ScaleLandmarks(VolumeLandmarkValues seeded, ExperienceLevel level)
    {
        ArgumentNullException.ThrowIfNull(seeded);

        var mev = ScaleLandmark(seeded.Mev, level);
        var mrv = Math.Max(mev + VolumeAdaptation.MinBandWidth, ScaleLandmark(seeded.Mrv, level));
        var mav = Math.Clamp(ScaleLandmark(seeded.Mav, level), mev + 1, mrv - 1);

        return new VolumeLandmarkValues(mev, mav, mrv);
    }
}
