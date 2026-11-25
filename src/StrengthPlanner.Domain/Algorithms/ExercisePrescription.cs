using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// What a block prescribes for one exercise in one week, together with the base range it was
/// prescribed from. The base is stored on the plan (<c>BaseRepRangeMin/Max</c>), because a
/// week's shifted range cannot be turned back into it.
/// </summary>
public sealed record ExercisePrescription(int BaseRepRangeMin, int BaseRepRangeMax, WeekPrescription Week);

/// <summary>
/// The one place that says what a block prescribes for an exercise in a week.
///
/// The generator used to work this out inline while it built a block, and adding an exercise to a
/// block that is already running needs exactly the same numbers. Two copies would drift (the shape
/// of defect rounds 9 and 12 kept finding), so both read this.
///
/// The prescription is decided by four things: the goal and the exercise's own type (the base rep
/// range, <see cref="GoalPrescriptions.ForExercise"/>), the experience level (the starting number
/// of sets), and the periodization model with the week (the shift from that base). A lifter's own
/// template may carry a rep range and a number of sets of its own; those replace the derived ones.
/// </summary>
public static class ExercisePrescriptions
{
    /// <summary>
    /// The prescription of <paramref name="weekNumber"/> for an exercise of this type.
    /// <paramref name="templateRepRangeMin"/>, <paramref name="templateRepRangeMax"/> and
    /// <paramref name="templateSets"/> are what a custom template said, or null when it said
    /// nothing, which is always the case for an exercise added to a running block.
    /// </summary>
    public static ExercisePrescription For(
        Goal goal,
        ExerciseType type,
        bool suitsLowReps,
        PeriodizationModel model,
        ExperienceLevel level,
        int weekNumber,
        int? templateRepRangeMin = null,
        int? templateRepRangeMax = null,
        int? templateSets = null)
    {
        // Izolacija u oba bloka ide 10-20, jer se snaga izražava u složenim pokretima; složena vežba koja
        // nizak opseg ne podnosi ostaje na 8-12. Vidi GoalPrescriptions.ForExercise.
        var exerciseSettings = GoalPrescriptions.ForExercise(goal, type, suitsLowReps);
        var baseRepRangeMin = templateRepRangeMin ?? exerciseSettings.RepRangeMin;
        var baseRepRangeMax = templateRepRangeMax ?? exerciseSettings.RepRangeMax;

        var week = Periodization.ForWeek(
            model,
            level,
            weekNumber,
            baseRepRangeMin,
            baseRepRangeMax,
            // RIR ostaje iz cilja, a ne iz vežbe: jedan ciljni RIR po bloku je ono što deload i povratak
            // oslobođene nedelje čitaju.
            GoalPrescriptions.ForGoal(goal).TargetRir,
            templateSets ?? ExperienceProgramming.StartingSetsPerExercise(level));

        return new ExercisePrescription(baseRepRangeMin, baseRepRangeMax, week);
    }
}
