namespace StrengthPlanner.Application.DTOs.OneRepMax;

public class OneRepMaxDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string Exercise { get; set; } = string.Empty;
    public decimal ValueKg { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// The value the planner starts from is older than the lookback window, so it came from
    /// the stale fallback. Set only on the current values, where that question has an answer.
    /// </summary>
    public bool IsOutsideLookback { get; set; }
}
