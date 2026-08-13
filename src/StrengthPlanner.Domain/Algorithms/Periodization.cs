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
/// Three models cover three ways of moving that balance:
///
/// <list type="bullet">
/// <item><b>Flat</b> — the same prescription every week over four weeks. Progress comes
/// from double progression, not from the schedule. This is what the system did before
/// models existed, so it stays the default.</item>
/// <item><b>Linear</b> — volume first (more reps, easier sets), intensity last (fewer
/// reps, closer to failure). The classic shape for a block leading to strength.</item>
/// <item><b>Inverse</b> — the same two ends in the opposite order: heavy while fresh,
/// volume once load has already driven fatigue up.</item>
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
    /// </summary>
    public const int MaxReps = TrainingConstants.EpleyRepCap;

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

    /// <summary>Below two sets an exercise stops being trained.</summary>
    public const int MinSets = 2;

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
        PeriodizationModel.Inverse => InverseWeeks,
        _ => FlatWeeks
    };

    /// <summary>How many weeks a block of this model runs.</summary>
    public static int DurationWeeks(PeriodizationModel model)
    {
        return ShapesFor(model).Length;
    }

    /// <summary>
    /// One week's prescription; <paramref name="weekNumber"/> counts from 1.
    ///
    /// A deload week keeps the goal's rep range and RIR and halves the sets. Its load is
    /// set separately, to 90% of what was actually used, once the previous week finishes.
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
                TargetRir: baseTargetRir);
        }

        // Opseg se pomera kao celina, ali donja granica ne sme ispod tri ponavljanja.
        // Kod snage (3-6) to znači da faza intenziteta ostaje uska — tamo se intenzitet
        // podiže RIR-om, jer niže od tri ponavljanja blok više nije hipertrofija.
        var repRangeMin = Math.Clamp(baseRepRangeMin + shape.RepShift, MinReps, MaxReps - 1);
        var repRangeMax = Math.Clamp(
            baseRepRangeMax + shape.RepShift,
            repRangeMin + 1,
            MaxReps);

        return new WeekPrescription(
            weekNumber,
            IsDeload: false,
            Sets: Math.Max(MinSets, baseSets + shape.SetShift),
            RepRangeMin: repRangeMin,
            RepRangeMax: repRangeMax,
            TargetRir: Math.Clamp(baseTargetRir + shape.RirShift, MinRir, MaxRir));
    }

    /// <summary>
    /// How many sets a training week carries, given the block's base set count.
    /// Throws for a deload week, whose halving cannot be inverted.
    /// </summary>
    public static int SetsForWeek(PeriodizationModel model, int weekNumber, int baseSets)
    {
        return ForWeek(model, weekNumber, MinReps, MinReps + 1, MinRir, baseSets).Sets;
    }

    /// <summary>
    /// Recovers the block's base set count from what a training week actually carries.
    ///
    /// The deload logic needs it: it has stored plans, not the profile that produced them,
    /// and reading the experience level again would silently re-shape a block in progress
    /// for anyone who changed their level part-way through.
    /// </summary>
    public static int BaseSetsFrom(PeriodizationModel model, int weekNumber, int weekSets)
    {
        var shapes = ShapesFor(model);

        if (weekNumber < 1 || weekNumber > shapes.Length || shapes[weekNumber - 1].IsDeload)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                weekNumber,
                "Base sets can only be recovered from a training week of this block.");
        }

        return Math.Max(MinSets, weekSets - shapes[weekNumber - 1].SetShift);
    }

    /// <summary>A deload halves the sets, but never below one.</summary>
    public static int DeloadSets(int baseSets)
    {
        return Math.Max(1, (int)Math.Ceiling(baseSets / 2m));
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
