using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>What one week of a block prescribes: sets, rep range and target RIR.</summary>
public sealed record WeekPrescription(
    int WeekNumber,
    bool IsDeload,
    int Sets,
    int RepRangeMin,
    int RepRangeMax,
    int TargetRir);

/// <summary>
/// Spreads the prescription across the weeks of a block.
///
/// Every week used to carry the same prescription, with a deload at the end as the only
/// difference. The handbook answers that directly: <i>"Ne možeš isto trenirati svake
/// nedelje i očekivati da napreduješ — telo se prilagodi. Zato se kroz blok menja odnos
/// volumena i intenziteta."</i>
///
/// Four models cover the ways of moving that balance:
///
/// <list type="bullet">
/// <item><b>Flat</b> — the same prescription every week over four weeks. Progress comes
/// from double progression, not from the schedule. This is what the system did before
/// models existed, so it is the <i>stored</i> default: <c>Mesocycle.PeriodizationModel</c>
/// falls back to it, which is what keeps blocks created before models existed reading
/// correctly. It is <b>not</b> what a new plan gets - the wizard proposes
/// <see cref="SuggestedModel"/>, and flat only when the lifter picks it.</item>
/// <item><b>LinearRising</b> — the handbook's linear model: sets rise through the block
/// while reps and reserve fall, so volume and intensity climb together toward the deload.
/// What the wizard offers as "linear".</item>
/// <item><b>Linear</b> — classical linear periodization, volume first (more reps and
/// sets, easier) and intensity last. Kept for the blocks that carry it; no longer offered.</item>
/// <item><b>Inverse</b> — heavy while fresh, volume once load has already driven fatigue
/// up; reps rise through the block.</item>
/// </list>
///
/// A week is expressed as a <b>shift from the goal's base</b> rather than as absolute
/// numbers, which is what lets one schedule serve both strength (3-6 reps) and hypertrophy
/// (8-12) while the experience level still sets the starting number of sets.
/// </summary>
public static class Periodization
{
    /// <summary>Reps added during the volume phase.</summary>
    public const int VolumeRepShift = 3;

    /// <summary>Reps removed during the transition into the intensity phase.</summary>
    public const int TransitionRepShift = 1;

    /// <summary>Reps removed during the intensity phase.</summary>
    public const int IntensityRepShift = 2;

    /// <summary>Fewest reps a week may prescribe.</summary>
    public const int MinReps = 3;

    /// <summary>
    /// Most reps a week may prescribe.
    ///
    /// Tied to the Epley cap on purpose. Sets logged above it produce no e1RM estimate,
    /// and three separate things read that estimate: the strength trend, personal-record
    /// detection, and the e1RM term of the fatigue score. A volume week pushed past the cap
    /// would therefore go dark exactly where the block is heaviest — the plan would look
    /// fine while the system stopped measuring it.
    ///
    /// The cap therefore <b>moves</b> a week rep window instead of narrowing it, and what
    /// it swallows comes back as a set — see <see cref="CappedShiftSetBonus"/>. A
    /// hypertrophy block starts at 8-12, flush against the cap, so the volume phase used to
    /// come out as 11-12: a two-rep window that raised the floor by three reps while
    /// pretending to be the easier week, and left double progression nothing to climb.
    ///
    /// It is the cap of a range that ends at or below it. An isolation range ends at
    /// <see cref="IsolationMaxReps"/>, and there the cap follows - see <see cref="MaxRepsFor"/>.
    /// </summary>
    public const int MaxReps = TrainingConstants.EpleyRepCap;

    /// <summary>Most reps a week may prescribe to an isolation exercise.</summary>
    public const int IsolationMaxReps = TrainingConstants.IsolationMaxReps;

    /// <summary>
    /// The rep cap for an exercise whose base range ends at <paramref name="baseRepRangeMax"/>.
    ///
    /// Read from the range rather than from the exercise type, for the same reason
    /// <c>ExercisePlan.BaseRepRangeMin/Max</c> exist: the plan carries its range, and a block
    /// generated before isolations had their own keeps reading the cap it was written with
    /// (an 8-12 isolation keeps 12). A range can only end above <see cref="MaxReps"/> when it
    /// is an isolation's: built-in compounds end at 12 at most, and a custom template refuses
    /// more for a compound. That range has given up the e1RM at its base already, so a cap of
    /// twelve would protect no measurement - it would only squeeze the window.
    /// </summary>
    public static int MaxRepsFor(int baseRepRangeMax)
    {
        return baseRepRangeMax > MaxReps ? IsolationMaxReps : MaxReps;
    }

    /// <summary>
    /// Lowest target RIR a week may prescribe, and it is deliberately not zero.
    ///
    /// Fatigue is measured as the shortfall between the target RIR and what the lifter
    /// actually managed. Below a target of zero there is no shortfall to measure — reps in
    /// reserve cannot go negative — so a week prescribed to failure silently drops the
    /// largest term of the fatigue score and can never trigger an early deload. Leaving one
    /// rep in reserve keeps the signal readable.
    /// </summary>
    public const int MinRir = 1;

    /// <summary>Above four reps in reserve a set stops driving adaptation.</summary>
    public const int MaxRir = 4;

    /// <summary>
    /// Reps in reserve a deload week adds to the goal target.
    ///
    /// A deload halves the sets and drops the load to 90% of what was used, but it used to
    /// keep the goal RIR, and the two do not fit together. Ten percent of a one-rep max is
    /// worth about three effective reps by Epley, so the same rep range at 90% is reached
    /// with roughly three more in reserve: a hypertrophy deload prescribed at RIR 1 asked
    /// the lifter to come within one rep of failure on a load where that is no longer
    /// possible. With a 1RM of 130 kg the deload sits at 90 kg, and RIR 1 there means
    /// something close to a normal working set - which is what a deload is not.
    ///
    /// Two, not three, and clamped by <see cref="MaxRir"/>: understating the reserve keeps
    /// the week a training week rather than a warm-up, and the deload load is already set
    /// from what was really lifted rather than from an estimate.
    /// </summary>
    public const int DeloadRirShift = 2;

    /// <summary>Below two sets an exercise stops being trained.</summary>
    public const int MinSets = 2;

    /// <summary>
    /// Sets a week gains when <see cref="MaxReps"/> swallows its upward rep shift.
    ///
    /// A volume phase means more work at a greater distance from failure. Reps are the
    /// first lever, but a goal whose range already ends at the cap has none left — and a
    /// week that cannot express its own phase is a week the model does not use: with the
    /// window merely sliding back, the inverse block came out with two identical weeks
    /// (its base week and its transition week), which is what the narrowed window had been
    /// hiding. The work goes into sets instead, which the weekly volume target then follows.
    ///
    /// One set, however many reps were swallowed. Sets and reps are not interchangeable
    /// one for one, and the bonus is meant to keep the phase legible, not to reprice it.
    /// </summary>
    public const int CappedShiftSetBonus = 1;

    /// <summary>One week's shifts away from the goal's base prescription.</summary>
    private sealed record WeekShape(int RepShift, int RirShift, int SetShift, bool IsDeload = false);

    private static readonly WeekShape Base = new(0, 0, 0);
    private static readonly WeekShape Deload = new(0, 0, 0, IsDeload: true);

    // Ravan blok: tri iste nedelje pa deload — tačno ono što je sistem radio i ranije.
    private static readonly WeekShape[] FlatWeeks = [Base, Base, Base, Deload];

    // Linearan: volumen -> osnova -> intenzitet. RIR pada kroz blok, jer se serije
    // vode sve bliže otkazu kako se volumen povlači. Kod hipertrofije (osnovni RIR 1) taj
    // pad brzo udari u donju granicu, pa intenzitet dalje nose ponavljanja i serije.
    private static readonly WeekShape[] LinearWeeks =
    [
        new(VolumeRepShift, 1, 1),
        new(VolumeRepShift, 0, 1),
        Base,
        new(-TransitionRepShift, -1, 0),
        new(-IntensityRepShift, -1, -1),
        Deload
    ];

    // Linearan po priručniku: serije rastu (priručnik: 3 -> 4 -> 4 -> 5 -> 5, što ovo daje
    // tačno za srednji nivo), ponavljanja i RIR padaju. Rane nedelje su lakše po rezervi, ne
    // po ponavljanjima: hipertrofija već počinje na Epley granici (12), pa bi pomeraj
    // ponavljanja naviše Epley pretvorio u seriju više - baš u nedeljama koje treba da nose
    // manje serija. Rezerva raste za jedan, ne za dva: RIR 4 je granica na kojoj serija
    // prestaje da se broji kao ceo stimulus, a priručnik linearan blok počinje na RIR 2-3.
    private static readonly WeekShape[] LinearRisingWeeks =
    [
        new(0, 1, -1),
        new(0, 1, 0),
        Base,
        new(-TransitionRepShift, -1, 1),
        new(-IntensityRepShift, -1, 1),
        Deload
    ];

    // Obrnut: isti krajevi, obrnutim redom.
    private static readonly WeekShape[] InverseWeeks =
    [
        new(-IntensityRepShift, 1, -1),
        new(-IntensityRepShift, 0, -1),
        Base,
        new(TransitionRepShift, -1, 0),
        new(VolumeRepShift, -1, 1),
        Deload
    ];

    private static WeekShape[] ShapesFor(PeriodizationModel model) => model switch
    {
        PeriodizationModel.Linear => LinearWeeks,
        PeriodizationModel.LinearRising => LinearRisingWeeks,
        PeriodizationModel.Inverse => InverseWeeks,
        _ => FlatWeeks
    };

    /// <summary>
    /// The model a new block is offered first; the lifter can still pick any.
    ///
    /// The linear model, whatever the goal and level. The handbook calls it <i>"idealan za
    /// početnike"</i> and says the inverse model is <i>"idealna za izgradnju snage i
    /// izdržljivosti"</i>, but the literature is clearly on the other side for strength: the
    /// one direct trial found linear gave larger strength gains than reverse linear, and only
    /// linear raised fat-free mass (Prestes et al. 2009), and a systematic review concludes
    /// that reverse periodization is no more effective for maximal strength, and that the
    /// traditional direction is the more effective one for strength and hypertrophy
    /// (González-Ravé et al. 2022). What both compare is the direction of the reps - falling
    /// through the block or rising - and <see cref="PeriodizationModel.LinearRising"/> lowers
    /// them. For hypertrophy it is also the MEV-to-MRV accumulation the volume landmarks are
    /// built around.
    ///
    /// The suggestion used to be linear for strength and inverse for hypertrophy, with the
    /// claim that the inverse model suited muscle growth - which neither source supports.
    /// The inverse model stays on offer; the handbook's mixed model for advanced lifters is
    /// undulating periodization, which this application does not implement.
    /// </summary>
    public static PeriodizationModel SuggestedModel => PeriodizationModel.LinearRising;

    /// <summary>How many weeks a block of this model runs.</summary>
    public static int DurationWeeks(PeriodizationModel model)
    {
        return ShapesFor(model).Length;
    }

    /// <summary>
    /// One week's prescription; <paramref name="weekNumber"/> counts from 1.
    ///
    /// A deload week keeps the goal's rep range, halves the sets and leaves more in
    /// reserve (<see cref="DeloadRir"/>). Its load is set separately, to 90% of what was
    /// actually used, once the previous week finishes.
    /// </summary>
    public static WeekPrescription ForWeek(
        PeriodizationModel model,
        int weekNumber,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        var shapes = ShapesFor(model);

        if (weekNumber < 1 || weekNumber > shapes.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                weekNumber,
                $"Week number must fall inside the {shapes.Length}-week block.");
        }

        var shape = shapes[weekNumber - 1];

        if (shape.IsDeload)
        {
            return new WeekPrescription(
                weekNumber,
                IsDeload: true,
                Sets: DeloadSets(baseSets),
                RepRangeMin: baseRepRangeMin,
                RepRangeMax: baseRepRangeMax,
                TargetRir: DeloadRir(baseTargetRir));
        }

        var (repRangeMin, repRangeMax) = RepWindow(baseRepRangeMin, baseRepRangeMax, shape.RepShift);

        return new WeekPrescription(
            weekNumber,
            IsDeload: false,
            Sets: SetsFor(shape, baseRepRangeMax, baseSets),
            RepRangeMin: repRangeMin,
            RepRangeMax: repRangeMax,
            TargetRir: Math.Clamp(baseTargetRir + shape.RirShift, MinRir, MaxRir));
    }

    /// <summary>
    /// One week rep window: the whole range moves by the week shift, keeping its width.
    ///
    /// The two bounds are clamped for different reasons, and that is why they behave
    /// differently. <see cref="MaxReps"/> is a <i>measurement</i> limit — above it no e1RM
    /// can be read — so a window that would cross it slides back down and stays as wide as
    /// the range it came from. <see cref="MinReps"/> is a <i>training</i> decision: below
    /// three reps the block stops being what it says it is, so there the window really does
    /// narrow, and strength weeks lean on RIR for the rest of the intensity.
    ///
    /// The width may be zero. A lifter who prescribes 5 reps means five, not five or six.
    /// </summary>
    private static (int Min, int Max) RepWindow(int baseRepRangeMin, int baseRepRangeMax, int repShift)
    {
        var width = Math.Max(0, baseRepRangeMax - baseRepRangeMin);
        var max = Math.Clamp(baseRepRangeMax + repShift, MinReps, MaxRepsFor(baseRepRangeMax));

        return (Math.Clamp(max - width, MinReps, max), max);
    }

    /// <summary>
    /// A week set count: the shape own shift, plus the set the Epley cap owes it.
    /// </summary>
    private static int SetsFor(WeekShape shape, int baseRepRangeMax, int baseSets)
    {
        return Math.Max(MinSets, baseSets + SetShift(shape, baseRepRangeMax));
    }

    /// <summary>
    /// Sets this week stands away from the block base week. Not a constant of the shape
    /// any more: a week whose rep shift ran into <see cref="MaxReps"/> carries it as a set,
    /// so the shift can only be read together with the range it was applied to.
    /// </summary>
    private static int SetShift(WeekShape shape, int baseRepRangeMax)
    {
        var swallowed = shape.RepShift > 0 && baseRepRangeMax + shape.RepShift > MaxRepsFor(baseRepRangeMax);

        return shape.SetShift + (swallowed ? CappedShiftSetBonus : 0);
    }

    /// <summary>
    /// The week of this block whose prescription <b>is</b> the base: no rep shift, no RIR
    /// shift, no set shift.
    ///
    /// Reading the base set count from that week is strictly better than inverting a
    /// shift, and the reason is the stored data. A plan row written by an earlier version
    /// of this file carries the set count that version prescribed, and inverting today's
    /// shift out of it recovers a base the block never had. Measured on the development
    /// database: 440 rows sit in a week whose shift today includes
    /// <see cref="CappedShiftSetBonus"/>, and 392 of them were written before that bonus
    /// existed - 168 of those carry a rep window (11-15) that today's code cannot even
    /// produce. No migration can tell those versions apart with confidence, and a restored
    /// planned deload carries another week's phase entirely. The base week needs none of
    /// that: no version of this file ever shifted it.
    /// </summary>
    public static int BaseWeekNumber(PeriodizationModel model)
    {
        var shapes = ShapesFor(model);

        for (var index = 0; index < shapes.Length; index++)
        {
            var shape = shapes[index];

            if (!shape.IsDeload && shape.RepShift == 0 && shape.RirShift == 0 && shape.SetShift == 0)
            {
                return index + 1;
            }
        }

        throw new InvalidOperationException(
            $"Periodization model {model} has no week that carries the base prescription.");
    }

    /// <summary>
    /// Recovers the block's base set count from what a training week actually carries.
    ///
    /// Prefer <see cref="BaseWeekNumber"/> and read the base week directly; this is the
    /// fallback for when that week is itself a deload. It cannot undo either clamp: a week
    /// pinned at <see cref="MinSets"/> could have come from two different bases (2 and 3
    /// both prescribe 2 sets in a week that removes one), and a row written by an older
    /// rule carries a shift that is not today's.
    ///
    /// The deload logic needs it: it has stored plans, not the profile that produced them,
    /// and reading the experience level again would silently re-shape a block in progress
    /// for anyone who changed their level part-way through.
    ///
    /// <paramref name="baseRepRangeMax"/> is what keeps it invertible. Since the Epley cap
    /// can turn a rep shift into a set (<see cref="CappedShiftSetBonus"/>), the same week
    /// number carries a different set shift for a hypertrophy exercise than for a strength
    /// one, and only the range it was prescribed from says which. This is the same reason
    /// <c>ExercisePlan.BaseRepRangeMin/Max</c> are stored at all: a clamp cannot be undone
    /// from its own result.
    /// </summary>
    public static int BaseSetsFrom(
        PeriodizationModel model,
        int weekNumber,
        int weekSets,
        int baseRepRangeMax)
    {
        var shapes = ShapesFor(model);

        if (weekNumber < 1 || weekNumber > shapes.Length || shapes[weekNumber - 1].IsDeload)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                weekNumber,
                "Base sets can only be recovered from a training week of this block.");
        }

        return Math.Max(MinSets, weekSets - SetShift(shapes[weekNumber - 1], baseRepRangeMax));
    }

    /// <summary>A deload halves the sets, but never below one.</summary>
    public static int DeloadSets(int baseSets)
    {
        return Math.Max(1, (int)Math.Ceiling(baseSets / 2m));
    }

    /// <summary>
    /// Target RIR of a deload week, given the goal it deloads from.
    /// See <see cref="DeloadRirShift"/> for why it is not the goal RIR itself.
    /// </summary>
    public static int DeloadRir(int baseTargetRir)
    {
        return Math.Clamp(baseTargetRir + DeloadRirShift, MinRir, MaxRir);
    }

    /// <summary>
    /// Whether a block of this model, for a lifter of this level, ends in a planned deload.
    ///
    /// Every block does, except a beginner's flat block. Of deloads the handbook says
    /// <i>"Početnici ne treba da razmišljaju o ovome"</i>, and a deload week in the middle of
    /// a nine-week program added nothing to hypertrophy and slightly reduced lower-body
    /// strength (Coleman et al. 2024). In a four-week flat block the planned deload was a
    /// quarter of a beginner's training time; there it becomes a fourth training week.
    ///
    /// A periodized block keeps it. The handbook's own linear and inverse schemes end in
    /// <i>"Nedelja 6: DELOAD"</i>, and the block's hardest weeks lead into it - which is also
    /// why a beginner, who gets no fatigue-driven deload
    /// (<see cref="ExperienceProgramming.DeloadThreshold"/>), keeps this one.
    /// </summary>
    public static bool HasPlannedDeload(PeriodizationModel model, ExperienceLevel level)
    {
        return !(level == ExperienceLevel.Beginner && model == PeriodizationModel.Flat);
    }

    /// <summary>
    /// One week's prescription for a lifter of this level: the same as
    /// <see cref="ForWeek(PeriodizationModel, int, int, int, int, int)"/>, except that a block
    /// without a planned deload (<see cref="HasPlannedDeload"/>) trains its deload week at the
    /// base prescription. The block keeps its length, so a plan's dates do not move.
    /// </summary>
    public static WeekPrescription ForWeek(
        PeriodizationModel model,
        ExperienceLevel level,
        int weekNumber,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        var week = ForWeek(model, weekNumber, baseRepRangeMin, baseRepRangeMax, baseTargetRir, baseSets);

        if (!week.IsDeload || HasPlannedDeload(model, level))
        {
            return week;
        }

        return ForWeek(model, BaseWeekNumber(model), baseRepRangeMin, baseRepRangeMax, baseTargetRir, baseSets)
            with { WeekNumber = weekNumber };
    }

    /// <summary>The whole block, week by week, for a lifter of this level.</summary>
    public static IReadOnlyList<WeekPrescription> ForBlock(
        PeriodizationModel model,
        ExperienceLevel level,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        return Enumerable
            .Range(1, DurationWeeks(model))
            .Select(weekNumber => ForWeek(
                model,
                level,
                weekNumber,
                baseRepRangeMin,
                baseRepRangeMax,
                baseTargetRir,
                baseSets))
            .ToList();
    }

    /// <summary>The whole block, week by week.</summary>
    public static IReadOnlyList<WeekPrescription> ForBlock(
        PeriodizationModel model,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        return Enumerable
            .Range(1, DurationWeeks(model))
            .Select(weekNumber => ForWeek(
                model,
                weekNumber,
                baseRepRangeMin,
                baseRepRangeMax,
                baseTargetRir,
                baseSets))
            .ToList();
    }
}
