namespace StrengthPlanner.Application.DTOs.Analytics;

/// <summary>
/// Training week by week across all plans and blocks, oldest first. Weeks with no training are
/// kept, so a gap shows as a gap.
/// </summary>
public class TrendDto
{
    public List<TrendWeekDto> Weeks { get; set; } = new();

    /// <summary>The muscles that were trained in the range, by name.</summary>
    public List<string> Muscles { get; set; } = new();
}

public class TrendWeekDto
{
    /// <summary>The Monday of the calendar week, midnight UTC.</summary>
    public DateTime WeekStart { get; set; }

    public decimal TonnageKg { get; set; }

    /// <summary>Every logged set of the week.</summary>
    public int Sets { get; set; }

    /// <summary>Sets that count toward volume, with the same credit as the weekly volume screen.</summary>
    public decimal StimulativeSets { get; set; }

    /// <summary>Stimulative sets by muscle name; a muscle takes its share of each set.</summary>
    public Dictionary<string, decimal> StimulativeSetsByMuscle { get; set; } = new();

    /// <summary>The block with most of the week's sets; null for a week with no training.</summary>
    public string? BlockName { get; set; }

    public string? PlanName { get; set; }

    /// <summary>At least half of the week's sets were done in a deload week.</summary>
    public bool IsDeload { get; set; }

    /// <summary>The week is the first one of its block in this range.</summary>
    public bool BlockStarts { get; set; }
}
