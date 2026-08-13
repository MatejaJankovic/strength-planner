using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.DTOs.Macrocycles;

public class MacrocycleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; }
    public List<MacrocycleBlockDto> Blocks { get; set; } = new();
}

public class MacrocycleBlockDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public Goal Goal { get; set; }
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>Model periodizacije za ovaj blok.</summary>
    public PeriodizationModel PeriodizationModel { get; set; }

    /// <summary>Koliko nedelja blok traje — zavisi od modela.</summary>
    public int DurationWeeks { get; set; }

    /// <summary>Naziv šablona za prikaz ("Push/Pull/Legs"), a ne ključ.</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Mezociklus generisan za ovaj blok; null dok blok nije došao na red.</summary>
    public Guid? MesocycleId { get; set; }

    /// <summary>"planned" (čeka red), "active" (u toku) ili "completed".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Broj završenih treninga i ukupan broj, za prikaz napretka.</summary>
    public int CompletedSessions { get; set; }
    public int TotalSessions { get; set; }
}
