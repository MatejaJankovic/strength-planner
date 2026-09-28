using StrengthPlanner.Application.DTOs.SetLogs;

namespace StrengthPlanner.Application.DTOs.Mesocycles;

public class ExercisePlanDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int Order { get; set; }

    /// <summary>Predloženi broj radnih serija — propis pomeren ka ciljnoj zoni volumena.</summary>
    public int TargetSets { get; set; }

    /// <summary>
    /// Broj serija koji propisuju nivo iskustva i periodizacija. Razlika u odnosu na
    /// <see cref="TargetSets"/> je tačno ono što je balansiranje volumena pomerilo.
    /// </summary>
    public int PrescribedSets { get; set; }

    public int RepRangeMin { get; set; }
    public int RepRangeMax { get; set; }
    public int TargetRir { get; set; }
    public decimal? TargetWeightKg { get; set; }

    /// <summary>
    /// Cilj nije došao iz odrađenih serija nego je izveden iz poznatog maksimuma.
    ///
    /// Tako stoji dok za tu vežbu nema odrađenog treninga od koga bi se cilj računao —
    /// najčešće zato što isti dan prethodne nedelje nije završen, ali i kada je maksimum
    /// unet pošto je blok već generisan. Čim taj trening postoji, progresija upisuje broj
    /// koji je vežbač zaista zaradio. Razlika se prikazuje, jer predlog iz procene i predlog
    /// iz odrađenog nisu isto jaka tvrdnja.
    ///
    /// Računa se samo za trening koji tek predstoji: završen i preskočen nose istoriju.
    /// </summary>
    public bool TargetWeightIsEstimate { get; set; }

    /// <summary>Korak kojim klijent pomera opterećenje za ovu vežbu (kg).</summary>
    public decimal WeightStepKg { get; set; }

    /// <summary>
    /// Vežba nosi deo telesne mase, pa je <see cref="TargetWeightKg"/> ono što se DODAJE
    /// (pojas, traka), a 0 znači „sopstvenom masom". Netačno i za vežbu sa telesnom masom
    /// ako profil nema unetu masu: tada se ne zna šta bi se dodavalo.
    /// </summary>
    public bool IsBodyweight { get; set; }

    /// <summary>Koliko kilograma telesne mase ova vežba nosi; 0 za spoljno opterećenje.</summary>
    public decimal BodyweightLoadKg { get; set; }
    public List<SetLogDto> SetLogs { get; set; } = new();
}
