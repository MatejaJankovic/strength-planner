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
/// intermediates <i>"veći volumen"</i>; the handbook's <i>"manji volumen, uz napredne
/// tehnike"</i> for advanced lifters is not followed, see
/// <see cref="StartingSetsPerExercise"/>;</item>
/// <item>where the volume landmarks start, because a beginner grows on less volume and
/// has less work capacity, and an advanced lifter needs more to keep growing;</item>
/// <item>whether fatigue should pull a deload forward at all — the handbook is blunt that
/// <i>"početnici ne treba da razmišljaju o ovome"</i>.</item>
/// </list>
/// </summary>
public static class ExperienceProgramming
{
    /// <summary>
    /// Working sets per exercise the plan starts with.
    ///
    /// Never fewer for a level whose volume landmarks sit higher (<see cref="LandmarkScale"/>).
    /// The advanced lifter used to start at three - the handbook's <i>"manji volumen, uz
    /// napredne tehnike"</i> - while their landmarks were scaled by 1.2, the highest of the
    /// three levels. The two pulled in opposite directions, and balancing patched the gap: in
    /// an advanced hypertrophy week of the built-in templates of three days or more it raised
    /// 155 of 195 exercises, 131 of them to the drift limit (80 at four sets). The handbook's
    /// smaller volume assumes the advanced techniques that carry the rest of the stimulus
    /// (drop sets, rest-pause), and
    /// this application does not model them; without them, fewer sets is simply less
    /// stimulus for the lifter who needs the most (Schoenfeld et al. 2019: in trained men,
    /// more sets gave more hypertrophy). Measured over every training week of the built-in
    /// templates of three days or more, the advanced hypertrophy prescription fell below MEV
    /// in 153 of 240 muscle-weeks on the flat model at three sets, and in 75 at four.
    /// </summary>
    public static int StartingSetsPerExercise(ExperienceLevel level) => level switch
    {
        // Srednji volumen uz visok intenzitet: napredak je još linearan i dolazi
        // od učenja pokreta, ne od gomilanja serija.
        ExperienceLevel.Beginner => 3,

        // Veći volumen — ovde je volumen glavna poluga napretka.
        ExperienceLevel.Intermediate => 4,

        // Isto koliko i srednji nivo: granice su mu više (×1.2), a napredne tehnike, uz
        // koje priručnik traži manji volumen, aplikacija ne modeluje.
        ExperienceLevel.Advanced => 4,

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
    /// Fewest compounds a strength session holds, whatever the level.
    /// </summary>
    public const int MinCompoundsInAStrengthSession = 2;

    /// <summary>
    /// Most compound exercises allowed in one session. This is the handbook's central
    /// distinction: compounds carry the largest stimulus but also the most fatigue, so
    /// the lifter who recovers best from them is the one who needs them most.
    ///
    /// A strength block never goes below <see cref="MinCompoundsInAStrengthSession"/>, which
    /// lifts only the advanced lifter's budget, from one to two. The handbook's <i>"do 3
    /// složene vežbe nedeljno"</i> for an advanced lifter describes a hypertrophy week, where
    /// isolation work carries the volume. Strength is specific to the lift and grows with
    /// how often the lift is trained: frequency raises strength independently of volume
    /// (Pelland et al. 2025), and it does so for multi-joint lifts rather than single-joint
    /// ones (Grgic et al. 2018). Measured before this rule, an advanced lifter's Upper/Lower
    /// strength block never rowed, and the two-day full-body block had no bench press.
    ///
    /// "One more for every level" was measured and rejected: a third compound for the
    /// intermediate lifter put the Upper/Lower strength prescription at 22 sets of quads
    /// against an MRV of 20, and a lifter who keeps the template's sets gets exactly that.
    /// </summary>
    public static int MaxCompoundsPerSession(ExperienceLevel level, Goal goal)
    {
        var budget = level switch
        {
            ExperienceLevel.Beginner => 3,
            ExperienceLevel.Intermediate => 2,
            ExperienceLevel.Advanced => 1,
            _ => 2
        };

        return goal == Goal.Strength
            ? Math.Max(budget, MinCompoundsInAStrengthSession)
            : budget;
    }

    /// <summary>
    /// Multiplier applied to the seeded MEV/MAV/MRV values.
    ///
    /// A beginner grows on less volume - untrained muscle responds to almost any stimulus -
    /// and has the least work capacity, so the whole band sits lower. An advanced lifter
    /// needs more volume to keep growing and tolerates it. Personal adaptation then moves
    /// from this starting point rather than from the population average.
    ///
    /// The reason used to be given the wrong way round: "a beginner's set is a weaker
    /// stimulus" would call for <i>more</i> sets, not fewer.
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
    /// Beginners get null on purpose. The handbook says of deloads that <i>"Početnici ne
    /// treba da razmišljaju o ovome"</i>, and in untrained men reduced-volume deloads neither
    /// helped nor hindered hypertrophy and strength endurance (Pancar et al. 2026), so there
    /// is nothing for a fatigue-driven one to gain. A periodized block keeps its planned
    /// deload; a flat one has none (<see cref="Periodization.HasPlannedDeload"/>).
    ///
    /// The reason used to be that beginners misjudge RIR. That is the handbook's claim, but
    /// the meta-analysis on RIR accuracy found no effect of training experience on it
    /// (Halperin et al. 2022), so it is no longer given as the reason.
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
