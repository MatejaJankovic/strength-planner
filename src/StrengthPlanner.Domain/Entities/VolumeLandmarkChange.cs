namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// How far one completed week moved one muscle's personal volume landmarks.
///
/// <see cref="UserVolumeLandmark"/> keeps only the current values, so a workout corrected
/// after its week was counted could not take that week's move back. This row is that move,
/// written when the week is counted and read when it has to be counted again. A week that
/// left a muscle's landmarks where they were has no row for it.
/// </summary>
public class VolumeLandmarkChange
{
    public Guid Id { get; set; }

    public Guid TrainingWeekId { get; set; }
    public TrainingWeek TrainingWeek { get; set; } = null!;

    public Guid MuscleGroupId { get; set; }
    public MuscleGroup MuscleGroup { get; set; } = null!;

    // Razlika posle - pre; negativna kad je nedelja spustila granicu.
    public int MevDelta { get; set; }
    public int MavDelta { get; set; }
    public int MrvDelta { get; set; }
}
