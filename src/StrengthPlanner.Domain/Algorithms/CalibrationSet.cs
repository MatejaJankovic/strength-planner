using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// The calibration set: in the last training week of a block, the first set of the session's
/// main lift goes to failure at the prescribed load.
///
/// Every other estimate of a maximum rests on the lifter's judgement of how many reps were
/// left, and that judgement is off by about one rep on average, with a wide spread between
/// lifters (Halperin et al. 2022). A set taken to failure has no reserve to judge, so its
/// estimate is the cleanest the plan ever gets. It is stored like any other estimate and
/// competes with them for the next block's start (<see cref="OneRepMaxBaseline"/>): the last
/// training week is where a block is most fatigued, so a reading that comes out lower than
/// the rest must not replace them, and one that comes out higher is already the best.
///
/// It applies only where the reading is possible and worth the effort:
/// <list type="bullet">
/// <item>not to a beginner, whose loads still rise every week and who gets nothing from a
/// test the next session repeats anyway;</item>
/// <item>only to the session's first exercise, and only to a compound that suits low reps,
/// so one hard set per session lands on a lift a maximum means something for;</item>
/// <item>only where the prescription can be read: at the prescribed load the set fails at
/// about <c>RepRangeMax + TargetRir</c> reps, and above
/// <see cref="TrainingConstants.EpleyRepCap"/> no estimate is taken at all. The last week of
/// a flat or inverse hypertrophy block (8-12 at RIR 1, the lowest target a week prescribes)
/// would fail near 13 and is skipped; the linear block every block is proposed with ends at
/// 6-10 at RIR 1, and every strength week fits.</item>
/// </list>
/// </summary>
public static class CalibrationSet
{
    /// <summary>Which set of the exercise is the calibration set.</summary>
    public const int SetNumber = 1;

    /// <summary>Whether an exercise's first set is prescribed to failure.</summary>
    public static bool AppliesTo(
        ExperienceLevel level,
        bool isLastTrainingWeek,
        int order,
        ExerciseType type,
        bool suitsLowReps,
        int repRangeMax,
        int targetRir)
    {
        return level != ExperienceLevel.Beginner
               && isLastTrainingWeek
               && order == 1
               && type == ExerciseType.Compound
               && suitsLowReps
               && ExpectedRepsAtFailure(repRangeMax, targetRir) <= TrainingConstants.EpleyRepCap;
    }

    /// <summary>
    /// Reps a set reaches at failure on a load prescribed for the top of the range at the
    /// target reserve.
    /// </summary>
    public static int ExpectedRepsAtFailure(int repRangeMax, int targetRir)
    {
        return repRangeMax + targetRir;
    }

    /// <summary>
    /// The last week of a block that is not a deload. A week counts as the last training week
    /// when no later week of the block trains, so an auto-deload that released the planned
    /// one moves the calibration with it.
    /// </summary>
    public static bool IsLastTrainingWeek(int weekNumber, bool isDeload, IEnumerable<(int WeekNumber, bool IsDeload)> weeks)
    {
        ArgumentNullException.ThrowIfNull(weeks);

        return !isDeload && !weeks.Any(week => !week.IsDeload && week.WeekNumber > weekNumber);
    }
}
