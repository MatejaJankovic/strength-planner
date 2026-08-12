namespace StrengthPlanner.Application.DTOs.Sessions;

public class CompleteSessionResultDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CompletedExerciseSummaryDto> Exercises { get; set; } = new();

    /// <summary>Popunjeno samo kada je ovaj trening zatvorio nedelju i pokrenuo deload.</summary>
    public AutoDeloadDto? AutoDeload { get; set; }

    /// <summary>Popunjeno kada je ovaj trening zatvorio blok i otvorio sledeći iz plana.</summary>
    public MacrocycleAdvanceDto? NextBlock { get; set; }
}

/// <summary>Prelazak na sledeći blok dugoročnog plana.</summary>
public class MacrocycleAdvanceDto
{
    public string PlanName { get; set; } = string.Empty;
    public int BlockOrder { get; set; }
    public int BlockCount { get; set; }

    /// <summary>Cilj novog bloka ("Strength" ili "Hypertrophy").</summary>
    public string Goal { get; set; } = string.Empty;

    public Guid MesocycleId { get; set; }
    public string MesocycleName { get; set; } = string.Empty;
}

/// <summary>Deload koji je uvela procena umora, a ne kalendar.</summary>
public class AutoDeloadDto
{
    /// <summary>Nedelja iz koje je umor izračunat.</summary>
    public int TriggeredByWeek { get; set; }

    /// <summary>Nedelja koja je pretvorena u deload.</summary>
    public int DeloadWeek { get; set; }

    /// <summary>Ocena umora, 0 (odmoran) do 1 (svi signali na maksimumu).</summary>
    public decimal FatigueScore { get; set; }

    /// <summary>
    /// Nedelja u kojoj je planirani deload otpao, jer mezociklus nosi samo jedan;
    /// null kada planiranog deload-a nije ni bilo ili je već započet.
    /// </summary>
    public int? PlannedDeloadReleasedWeek { get; set; }
}
