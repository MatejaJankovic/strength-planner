namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>One page of the lifter's finished and skipped workouts, newest first.</summary>
public class TrainingHistoryDto
{
    public List<HistoryEntryDto> Items { get; set; } = new();

    /// <summary>How many workouts there are in all, so the screen knows whether there is more.</summary>
    public int Total { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class HistoryEntryDto
{
    public Guid SessionId { get; set; }

    /// <summary>"Completed" or "Skipped".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When the workout was done: its completion time, or for older ones the time of its last
    /// set. For a workout with neither (a skipped one) it is the week it was planned for, and
    /// <see cref="DateKnown"/> is false.
    /// </summary>
    public DateTime Date { get; set; }

    public bool DateKnown { get; set; }

    public string DayLabel { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public bool IsDeload { get; set; }

    /// <summary>The plan the block belongs to; the block's own name when it stands alone.</summary>
    public string PlanName { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;

    /// <summary>The lifter's note about the workout; null when there is none.</summary>
    public string? Note { get; set; }

    public int SetCount { get; set; }

    /// <summary>Weight x reps over every set, with the body mass the movement carries.</summary>
    public decimal TonnageKg { get; set; }

    /// <summary>The exercises that have at least one set, in the order of the workout.</summary>
    public List<string> Exercises { get; set; } = new();
}
