namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Which of the two things set balancing keeps moved one exercise's proposal.
/// </summary>
public enum SetChangeReason
{
    /// <summary>The week was short of, or past, the muscle's weekly volume target.</summary>
    WeeklyTarget,

    /// <summary>
    /// One session carried more of the muscle than
    /// <c>TrainingConstants.MaxSetsPerMusclePerSession</c>. The week may well be below its
    /// target at the same time - that is exactly the case the other reason cannot explain.
    /// </summary>
    SessionCeiling
}
