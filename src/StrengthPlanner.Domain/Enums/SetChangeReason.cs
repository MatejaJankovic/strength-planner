namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Which of the two things set balancing keeps moved one exercise's proposal.
/// </summary>
public enum SetChangeReason
{
    /// <summary>
    /// The week's own limits: short of or past the muscle's volume target, or past its MRV.
    /// </summary>
    WeeklyTarget,

    /// <summary>
    /// Without the ceiling, balancing would have kept this exercise higher, and its session
    /// would have carried more of the muscle than
    /// <c>TrainingConstants.MaxSetsPerMusclePerSession</c>. The week may well be below its
    /// target at the same time - that is exactly the case the other reason cannot explain.
    /// </summary>
    SessionCeiling
}
