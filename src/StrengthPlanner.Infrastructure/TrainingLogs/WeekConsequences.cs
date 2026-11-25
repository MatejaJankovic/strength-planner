using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// What a workout does to the week around it, after its own exercises are settled: the volume
/// limits learn from the week, the fatigue score may pull a deload forward, and the sets of the
/// workouts still to come are balanced again.
///
/// Finishing a workout and saving a correction of one both need exactly this, in exactly this
/// order, so it lives in one place.
/// </summary>
public sealed class WeekConsequences
{
    private readonly VolumeLandmarkService _volumeLandmarks;
    private readonly DeloadService _deloads;
    private readonly WeeklySetPlanner _setPlanner;

    public WeekConsequences(
        VolumeLandmarkService volumeLandmarks,
        DeloadService deloads,
        WeeklySetPlanner setPlanner)
    {
        _volumeLandmarks = volumeLandmarks;
        _deloads = deloads;
        _setPlanner = setPlanner;
    }

    /// <summary>
    /// What the week's end does: the volume limits learn from every week that is fully done
    /// and not yet counted, and the fatigue score may pull a deload forward.
    ///
    /// Both are conditional UPDATEs on the week, so a week is counted exactly once. They stay
    /// in the same transaction as the completion, so either everything is written or nothing.
    /// The fatigue score goes AFTER the progression: a deload rewrites the loads that the
    /// progression has just filled in for the next week.
    /// </summary>
    public async Task<DeloadOutcome?> CloseAsync(
        Guid userId,
        Guid mesocycleId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _volumeLandmarks.AdaptPendingWeeksAsync(
            userId,
            mesocycleId,
            now,
            cancellationToken);

        return await _deloads.EvaluatePendingWeeksAsync(
            userId,
            mesocycleId,
            cancellationToken);
    }

    /// <summary>
    /// The set plan is recalculated AFTER the deload, and only once the deload is WRITTEN.
    /// Both parts of that order carry weight:
    ///
    /// After the deload, because a deload changes the prescription the balancing starts from.
    /// After the write, because balancing asks the database which weeks are deloads — and
    /// DeloadService leaves the conversion of a week in the change tracker. Without the
    /// SaveChanges before this call the query still sees the old state, takes the freshly
    /// relieved week for an ordinary one and gives it back four sets in place of the halved
    /// two: the user gets a "deload" with the volume of a full training week. Measured before
    /// the fix — the whole week went from two to four sets per exercise.
    ///
    /// This workout wrote part of the week's volume (or skipped it), so the workouts still to
    /// come in that week get a proposal that brings the week back into the target zone. A
    /// block that follows its template literally is not balanced here either. Had it been
    /// skipped only at generation, the first completed workout would have put the sets back on
    /// the target volume — and the choice would have held exactly until that first workout.
    /// </summary>
    public async Task<IReadOnlyList<SetAdjustment>> RebalanceAsync(
        Guid userId,
        Guid mesocycleId,
        SetAllocation allocation,
        CancellationToken cancellationToken)
    {
        if (allocation != SetAllocation.TargetVolume)
        {
            return [];
        }

        return await _setPlanner.RebalanceAsync(userId, mesocycleId, cancellationToken);
    }
}
