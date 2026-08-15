namespace StrengthPlanner.Domain.Algorithms;

/// <summary>How much one exercise loads one muscle group: 1.0 primary, 0.5 secondary.</summary>
/// <param name="MuscleGroupId">The muscle group the exercise loads.</param>
/// <param name="Contribution">Share of a set that counts toward that muscle's weekly volume.</param>
public sealed record MuscleLoad(Guid MuscleGroupId, decimal Contribution);

/// <summary>
/// One planned exercise whose set count the allocator is allowed to move.
/// </summary>
/// <param name="Id">Opaque identity of the plan; the allocator only hands it back.</param>
/// <param name="PrescribedSets">
/// What experience level and periodization prescribe for this exercise this week. It is
/// the anchor the result may drift around, never a value the allocator recomputes.
/// </param>
/// <param name="Muscles">Muscle groups this exercise loads, with their contributions.</param>
public sealed record ExerciseSetSlot(
    Guid Id,
    int PrescribedSets,
    IReadOnlyList<MuscleLoad> Muscles);

/// <summary>Where one muscle group's week should land, and where it must not go.</summary>
/// <param name="MuscleGroupId">The muscle group these limits belong to.</param>
/// <param name="TargetSets">MAV — the weekly volume actually worth aiming for.</param>
/// <param name="CeilingSets">MRV — above this the week outruns recovery.</param>
public sealed record MuscleVolumeTarget(Guid MuscleGroupId, decimal TargetSets, decimal CeilingSets);

/// <summary>
/// Chooses how many sets each exercise of a week gets, so that the week as a whole lands
/// in the optimal volume zone of every muscle it trains.
///
/// Until now the set count came from the experience level alone (three, four, three) and
/// periodization shifted it by one — the same number for every exercise, whatever it
/// trained and however often the week trained it. Weekly volume per muscle was therefore
/// whatever the template happened to add up to. The system already knew where that volume
/// <i>should</i> land: MAV is defined as <i>"optimalan volumen treninga pri kojem se telo
/// adekvatno oporavlja i stimuliše hipertrofiju"</i>, it is stored per muscle, and it is
/// learned from the lifter's own weeks. It simply never reached the plan.
///
/// This closes that loop. The prescription stays the anchor — the handbook's level rules
/// are not overridden, only nudged by at most <see cref="MaxDriftFromPrescription"/> sets —
/// and within that window the sets are moved to whichever exercises bring their muscles
/// closest to MAV.
///
/// Two properties fall out of measuring the cost per muscle rather than per exercise, and
/// both are the behaviour a coach would want:
///
/// <list type="bullet">
/// <item>Volume is added where it is cheapest. Raising chest by one set costs one set of
/// flyes but two of bench press (which drags shoulders and triceps up with it), so
/// isolation work absorbs the corrections and the compounds are left alone.</item>
/// <item>A muscle that cannot be reached is approached, not chased. A two-day full-body
/// week has nowhere near enough room for most muscles, and the allocator gets as close as
/// the window allows instead of failing or inflating one exercise.</item>
/// <item>A correction is spread over the exercises that can take it rather than emptied
/// into the first ones the search happens to reach — see <see cref="DriftPenalty"/>.</item>
/// </list>
/// </summary>
public static class WeeklySetAllocation
{
    /// <summary>
    /// Furthest the weekly volume target may pull an exercise away from its prescription.
    ///
    /// The prescription encodes the handbook's rule that the level, not the arithmetic,
    /// decides the shape of a session — <i>"napredni vežbač bi pregoreo od treninga
    /// početnika"</i>. Left unbounded, one large muscle group short of its MAV would pile
    /// sets onto a beginner's session until it stopped being a beginner's session.
    /// </summary>
    public const int MaxDriftFromPrescription = 2;

    /// <summary>Below this an exercise stops being trained, same bound periodization uses.</summary>
    public const int MinSetsPerExercise = Periodization.MinSets;

    /// <summary>
    /// Most sets one exercise carries in one session. Past roughly this point the sets
    /// stop adding stimulus and only add fatigue — the extra volume belongs on another
    /// exercise, or another day.
    /// </summary>
    public const int MaxSetsPerExercise = 6;

    /// <summary>
    /// How much heavier a set past MRV weighs than a set short of MAV.
    ///
    /// Missing the target costs progress; outrunning recovery costs the following weeks.
    /// The allocator will therefore leave a muscle well under its MAV rather than push any
    /// muscle over its MRV to get there.
    /// </summary>
    public const decimal CeilingPenalty = 4m;

    /// <summary>
    /// Small cost for standing away from the prescription, growing with the square of the
    /// distance.
    ///
    /// Without it the search cannot tell four exercises at four sets from two at six and
    /// two left at two — both land the week on the same total, so it takes whichever it
    /// reaches first, which is always the earliest exercises of the earliest day. Those are
    /// not the same week: the second dumps the entire correction on Monday and leaves
    /// Thursday short. Charging more for each further set spreads the correction across the
    /// exercises that can absorb it.
    ///
    /// It is deliberately too small to outvote the volume target. The most it can charge
    /// for one step is 0.3 (moving from one set of drift to two, the widest allowed), while
    /// the smallest improvement any move can make is 0.5 — half a set, the contribution of
    /// a secondary muscle.
    /// </summary>
    public const decimal DriftPenalty = 0.1m;

    // Zaštita, a ne granica pretrage: svaki korak strogo smanjuje cenu nad konačnim
    // skupom stanja, pa se petlja i sama zaustavlja. Ostaje da izmena funkcije cene
    // sutra ne bi mogla da je pretvori u beskonačnu.
    private const int MaxSteps = 1_000;

    private static readonly IReadOnlyDictionary<Guid, decimal> NoVolume = new Dictionary<Guid, decimal>();

    /// <summary>
    /// Sets per exercise for a week nothing has been logged in yet.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> Allocate(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyList<MuscleVolumeTarget> targets)
    {
        return Allocate(slots, targets, NoVolume, NoVolume);
    }

    /// <summary>
    /// Sets per exercise for the part of the week still to come.
    ///
    /// This is the same call for both jobs the feature does, which is why a mid-week
    /// correction cannot drift: the result is a function of the prescription and of what
    /// the week has actually banked, never of the previous answer. Re-running it after
    /// every session converges rather than compounding.
    ///
    /// The two completed measures are deliberately different, exactly as elsewhere in the
    /// system: what is still <i>missing</i> is measured in stimulative sets (a set stopped
    /// five reps short did not earn its place in the volume count), while the MRV ceiling
    /// is measured in raw sets, because recovery is spent by every set performed. Without
    /// the second measure a week of easy sets would read as a week of rest and the
    /// allocator would keep piling more on top of it.
    /// </summary>
    /// <param name="slots">Plans still open to change, in the order ties should be broken.</param>
    /// <param name="targets">MAV and MRV per muscle group the week touches.</param>
    /// <param name="completedStimulativeSets">Stimulative volume already banked this week.</param>
    /// <param name="completedRawSets">Every set already performed this week, however easy.</param>
    public static IReadOnlyDictionary<Guid, int> Allocate(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyList<MuscleVolumeTarget> targets,
        IReadOnlyDictionary<Guid, decimal> completedStimulativeSets,
        IReadOnlyDictionary<Guid, decimal> completedRawSets)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(completedStimulativeSets);
        ArgumentNullException.ThrowIfNull(completedRawSets);

        var sets = slots.ToDictionary(slot => slot.Id, slot => slot.PrescribedSets);

        if (slots.Count == 0 || targets.Count == 0)
        {
            return sets;
        }

        var targetByMuscleGroupId = targets.ToDictionary(target => target.MuscleGroupId);
        var bounds = slots.ToDictionary(slot => slot.Id, BoundsFor);

        // Planirana serija se broji kao puna: propisana je na ciljnom RIR-u, pa je
        // pretpostavka da će i biti odrađena tako. Odrađeni deo nedelje ulazi merom
        // koja mu pripada — otuda dve projekcije nad istim planom.
        var stimulative = Project(slots, sets, completedStimulativeSets);
        var raw = Project(slots, sets, completedRawSets);

        for (var step = 0; step < MaxSteps; step++)
        {
            ExerciseSetSlot? bestSlot = null;
            var bestDirection = 0;
            var bestDelta = 0m;

            // Redosled je redosled koji je pozivalac dao (dan, pa mesto u treningu), pa
            // dva jednako dobra poteza uvek završe istim izborom.
            foreach (var slot in slots)
            {
                var (lower, upper) = bounds[slot.Id];
                var current = sets[slot.Id];

                foreach (var direction in new[] { 1, -1 })
                {
                    var candidate = current + direction;
                    if (candidate < lower || candidate > upper)
                    {
                        continue;
                    }

                    var delta = CostDelta(
                        slot,
                        current,
                        direction,
                        stimulative,
                        raw,
                        targetByMuscleGroupId);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestSlot = slot;
                        bestDirection = direction;
                    }
                }
            }

            if (bestSlot is null)
            {
                return sets;
            }

            sets[bestSlot.Id] += bestDirection;

            foreach (var muscle in bestSlot.Muscles)
            {
                var moved = muscle.Contribution * bestDirection;
                stimulative[muscle.MuscleGroupId] = stimulative.GetValueOrDefault(muscle.MuscleGroupId) + moved;
                raw[muscle.MuscleGroupId] = raw.GetValueOrDefault(muscle.MuscleGroupId) + moved;
            }
        }

        return sets;
    }

    /// <summary>
    /// Weekly volume per muscle group that a set of choices adds up to, on top of whatever
    /// the week has already banked.
    /// </summary>
    public static Dictionary<Guid, decimal> Project(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyDictionary<Guid, int> setsBySlotId,
        IReadOnlyDictionary<Guid, decimal> completedSets)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(setsBySlotId);
        ArgumentNullException.ThrowIfNull(completedSets);

        var projected = new Dictionary<Guid, decimal>(completedSets);

        foreach (var slot in slots)
        {
            if (!setsBySlotId.TryGetValue(slot.Id, out var planned))
            {
                continue;
            }

            foreach (var muscle in slot.Muscles)
            {
                projected[muscle.MuscleGroupId] =
                    projected.GetValueOrDefault(muscle.MuscleGroupId) + (muscle.Contribution * planned);
            }
        }

        return projected;
    }

    /// <summary>
    /// The window one exercise may move inside.
    ///
    /// The prescription is always inside its own window, even when it already sits outside
    /// the absolute bounds — a deload's single set is not raised to two just because the
    /// allocator would not otherwise write one. Balancing volume may refuse to help; it
    /// may not overrule a decision taken elsewhere.
    /// </summary>
    private static (int Lower, int Upper) BoundsFor(ExerciseSetSlot slot)
    {
        var lower = Math.Min(
            slot.PrescribedSets,
            Math.Max(MinSetsPerExercise, slot.PrescribedSets - MaxDriftFromPrescription));
        var upper = Math.Max(
            slot.PrescribedSets,
            Math.Min(MaxSetsPerExercise, slot.PrescribedSets + MaxDriftFromPrescription));

        return (lower, upper);
    }

    /// <summary>
    /// How much moving one exercise by one set changes the cost of the whole week. Only
    /// the muscles that exercise loads can change, so the rest of the week is not re-summed.
    /// </summary>
    private static decimal CostDelta(
        ExerciseSetSlot slot,
        int currentSets,
        int direction,
        IReadOnlyDictionary<Guid, decimal> stimulative,
        IReadOnlyDictionary<Guid, decimal> raw,
        IReadOnlyDictionary<Guid, MuscleVolumeTarget> targetByMuscleGroupId)
    {
        var driftBefore = currentSets - slot.PrescribedSets;
        var driftAfter = driftBefore + direction;
        var delta = DriftPenalty * ((driftAfter * driftAfter) - (driftBefore * driftBefore));

        foreach (var muscle in slot.Muscles)
        {
            if (!targetByMuscleGroupId.TryGetValue(muscle.MuscleGroupId, out var target))
            {
                continue;
            }

            var moved = muscle.Contribution * direction;

            var currentStimulative = stimulative.GetValueOrDefault(muscle.MuscleGroupId);
            delta += Math.Abs(currentStimulative + moved - target.TargetSets)
                     - Math.Abs(currentStimulative - target.TargetSets);

            var currentRaw = raw.GetValueOrDefault(muscle.MuscleGroupId);
            delta += CeilingPenalty
                     * (Math.Max(0m, currentRaw + moved - target.CeilingSets)
                        - Math.Max(0m, currentRaw - target.CeilingSets));
        }

        return delta;
    }
}
