using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Which sessions a week is still waiting for.
///
/// A week is evaluated, and a block hands over to the next one, only once nothing is
/// pending. That used to mean "every session completed", so one workout the lifter never
/// did held the week open forever: it was never scored for fatigue, its volume never
/// taught the limits, and the plan's next block was never generated. Measured on the
/// development database, nine weeks sat in exactly that state — a hole, with a later week
/// already worked through.
/// </summary>
public static class SessionLifecycle
{
    /// <summary>
    /// The statuses that mean a session has produced everything it ever will.
    ///
    /// Kept as a set rather than as a written-out condition because the database asks the
    /// same question: a query can translate <c>Contains</c>, so both sides read one list
    /// instead of repeating a boolean that would drift the day a status is added.
    /// </summary>
    public static readonly SessionStatus[] Settled =
    [
        SessionStatus.Completed,
        SessionStatus.Skipped
    ];

    /// <summary>Nothing more is owed on this session.</summary>
    public static bool IsSettled(SessionStatus status) => Settled.Contains(status);

    /// <summary>The week is still waiting for this session.</summary>
    public static bool IsPending(SessionStatus status) => !IsSettled(status);

    /// <summary>
    /// Only a session that has not been touched may be skipped. One that is under way or
    /// finished carries logged sets, and skipping it would have to decide what to do with
    /// them; there is no answer to that which is not a lie about what happened.
    /// </summary>
    public static bool CanSkip(SessionStatus status) => status == SessionStatus.Planned;

    /// <summary>Putting a skipped session back on the plan.</summary>
    public static bool CanUnskip(SessionStatus status) => status == SessionStatus.Skipped;

    /// <summary>Nothing is pending, so the week may be judged and the block may move on.</summary>
    public static bool IsOver(IEnumerable<SessionStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.All(IsSettled);
    }

    /// <summary>
    /// Whether the week may teach the volume limits.
    ///
    /// Stricter than <see cref="IsOver"/> on purpose: a week with a skipped day did less
    /// work than it prescribed, and how much volume a lifter needs cannot be read from a
    /// week that did not happen. Silence is not a measurement — the same rule the strength
    /// signal follows.
    /// </summary>
    public static bool TeachesVolume(IEnumerable<SessionStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.All(status => status == SessionStatus.Completed);
    }
}
