namespace StrengthPlanner.Application.DTOs.Analytics;

public class PersonalRecordDto
{
    public Guid ExerciseId { get; set; }
    public string Exercise { get; set; } = string.Empty;
    public decimal? BestE1Rm { get; set; }
    public decimal? BestWeight { get; set; }
    public DateTime? AchievedAt { get; set; }

    /// <summary>
    /// Vežba nosi deo telesne mase, pa su oba rekorda UKUPNO opterećenje (telo + dodato).
    /// Zgib bez pojasa bi kao „najveća težina" inače prijavio 0 kg, dok mu e1RM stoji na
    /// 120 — dva broja u istom redu, u različitim jedinicama.
    /// </summary>
    public bool IsBodyweight { get; set; }
}
