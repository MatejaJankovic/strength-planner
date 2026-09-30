using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>Base rep range and target RIR a goal prescribes, before periodization shifts it.</summary>
public sealed record GoalPrescription(int RepRangeMin, int RepRangeMax, int TargetRir);

/// <summary>
/// Translates a training goal into the rep range and proximity to failure it asks for.
///
/// Strength lives in low reps against heavy load; hypertrophy in the middle range with a
/// rep or two left in reserve. Both numbers are training rules, not configuration, which is
/// why they live in the domain: the generator writes them into the first block, and the
/// deload logic needs the same values later to work out what a week should look like once
/// fatigue has rearranged the block.
/// </summary>
public static class GoalPrescriptions
{
    public static GoalPrescription ForGoal(Goal goal) => goal switch
    {
        Goal.Strength => new GoalPrescription(RepRangeMin: 3, RepRangeMax: 6, TargetRir: 2),
        Goal.Hypertrophy => new GoalPrescription(RepRangeMin: 8, RepRangeMax: 12, TargetRir: 1),
        _ => throw new ArgumentOutOfRangeException(nameof(goal), goal, "Unsupported training goal.")
    };

    /// <summary>
    /// The same prescription, narrowed to what one exercise should actually carry.
    ///
    /// A block goal describes how the lifter intends to get stronger, and strength is
    /// expressed in the movements that can carry load. An isolation exercise cannot: a set
    /// of three lateral raises is not a test of force production, it is a way to load a
    /// shoulder joint with a weight the muscle was never the limiting factor for. The
    /// handbook puts compounds and isolations on opposite sides of exactly this line, and
    /// an advanced lifter's strength session is one compound and five isolations - all of
    /// which used to be prescribed at 3-6 reps, and at 3-4 in the intensity week.
    ///
    /// So the rep range follows the exercise and the target RIR follows the block. Reserve
    /// is how hard the week is meant to be, and that is a property of the week, not of the
    /// movement; keeping it from the goal is also what lets the deload restore a plan from
    /// one number per block.
    ///
    /// The same holds for a compound that cannot carry a low range
    /// (<paramref name="suitsLowReps"/> false) - see <see cref="CarriesTheGoalRange"/>.
    ///
    /// An isolation exercise carries its own range in either block,
    /// <see cref="TrainingConstants.IsolationRepRangeMin"/> to
    /// <see cref="TrainingConstants.IsolationMaxReps"/>. It used to take 8-12, the hypertrophy
    /// range of the compounds, in both blocks.
    /// </summary>
    public static GoalPrescription ForExercise(Goal goal, ExerciseType type, bool suitsLowReps)
    {
        var goalPrescription = ForGoal(goal);

        if (type == ExerciseType.Isolation)
        {
            return goalPrescription with
            {
                RepRangeMin = TrainingConstants.IsolationRepRangeMin,
                RepRangeMax = TrainingConstants.IsolationMaxReps
            };
        }

        if (CarriesTheGoalRange(goal, type, suitsLowReps))
        {
            return goalPrescription;
        }

        // Složena vežba koja ne podnosi nizak opseg: pomoćni rad u opsegu hipertrofije.
        var accessory = ForGoal(Goal.Hypertrophy);

        return goalPrescription with
        {
            RepRangeMin = accessory.RepRangeMin,
            RepRangeMax = accessory.RepRangeMax
        };
    }

    /// <summary>
    /// Most reps a custom template may prescribe to an exercise of this type: the isolation
    /// range's top for an isolation, and the Epley cap for a compound - whose sets are read
    /// for an estimate by the strength trend, the records and the fatigue score.
    /// </summary>
    public static int MaxTemplateReps(ExerciseType type)
    {
        return type == ExerciseType.Isolation ? TrainingConstants.IsolationMaxReps : TrainingConstants.EpleyRepCap;
    }

    /// <summary>
    /// Whether this exercise carries the block's own rep range: every compound in a
    /// hypertrophy block, and in a strength block only a compound that can be loaded for a
    /// set of three to six. An isolation never does - it has a range of its own.
    ///
    /// Not every compound can. The handbook says unilateral work is <i>"nije idealna za
    /// razvoj apsolutne snage"</i> and that a low range <i>"može narušiti tehniku, naročito
    /// kod vežbi sa nestabilnim uslovima"</i>; a goblet squat is limited by the heaviest
    /// dumbbell a lifter can hold, and a push-up has no load to add. Measured before this
    /// rule, an advanced lifter's Legs Specialization strength block put three of its five
    /// compound slots - Bulgarian split squat, single-leg RDL, step-up - at 3-6 reps, and no
    /// bilateral squat or hinge at all. Those exercises now keep the accessory range, and
    /// set balancing is free to move them like any other accessory.
    /// </summary>
    public static bool CarriesTheGoalRange(Goal goal, ExerciseType type, bool suitsLowReps)
    {
        return type == ExerciseType.Compound && (goal != Goal.Strength || suitsLowReps);
    }

    /// <summary>
    /// A catalog lift that a strength block programs as a strength lift: a compound that can
    /// be loaded for three to six. It decides a built-in template's rep range and which lifts
    /// open a strength session; <see cref="IsMainLift"/> decides what balancing may move.
    /// </summary>
    public static bool IsStrengthLift(Goal goal, ExerciseType type, bool suitsLowReps)
    {
        return goal == Goal.Strength && type == ExerciseType.Compound && suitsLowReps;
    }

    /// <summary>
    /// A planned exercise that carries a strength block's own prescription: a compound whose
    /// base range sits in the strength range. Its sets are the block's work, so volume
    /// balancing leaves them where periodization put them
    /// (<see cref="ExerciseSetSlot.IsMainLift"/>) and moves the accessory work around them.
    ///
    /// Read from the plan's range rather than from the catalog, because a custom template
    /// sets its own: a leg press the lifter entered at 8-12 is accessory work in their
    /// strength block, and a split squat they entered at 3-5 is the lift they chose to load.
    /// For a block generated from a built-in template by this version the two readings agree -
    /// its range comes from <see cref="ForExercise"/>, which gives 3-6 exactly to
    /// <see cref="IsStrengthLift"/>. A strength block generated earlier prescribed 3-6 to its
    /// unilateral lifts as well, and there they are main lifts: the block's own prescription
    /// says so, and balancing it by a rule the block was not written with would be the
    /// round-12 mistake of re-shaping a block in progress.
    /// </summary>
    public static bool IsMainLift(Goal goal, ExerciseType type, int baseRepRangeMax)
    {
        return goal == Goal.Strength
               && type == ExerciseType.Compound
               && baseRepRangeMax <= ForGoal(Goal.Strength).RepRangeMax;
    }
}
