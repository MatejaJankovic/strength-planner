namespace StrengthPlanner.Application.DTOs.Analytics;

public class WeeklyVolumeDto
{
    public string Muscle { get; set; } = string.Empty;
    public decimal Sets { get; set; }
    public int Mev { get; set; }
    public int Mrv { get; set; }

    /// <summary>Populaciona seed granica — vrednost na koju reset vraća.</summary>
    public int DefaultMev { get; set; }
    public int DefaultMrv { get; set; }

    /// <summary>True kada su granice naučene iz korisnikovog odgovora na volumen.</summary>
    public bool IsPersonal { get; set; }

    public string Status { get; set; } = string.Empty;
}
