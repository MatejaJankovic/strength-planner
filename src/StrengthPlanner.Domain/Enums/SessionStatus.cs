namespace StrengthPlanner.Domain.Enums;

public enum SessionStatus
{
    Planned,
    InProgress,
    Completed,

    /// <summary>
    /// The lifter said this session will not happen.
    ///
    /// Not the same as a session that simply has not been done yet. A planned session is
    /// still owed, and everything that asks "is this week over" waits for it; a skipped one
    /// is an answer, so the week can close and the block can move on. It stays reversible —
    /// nothing is deleted, the prescription is untouched, and unskipping puts it back on
    /// the plan.
    ///
    /// Stored as text (WorkoutSessionConfiguration uses HasConversion&lt;string&gt;), so
    /// adding a member here does not renumber anything already written.
    /// </summary>
    Skipped
}
