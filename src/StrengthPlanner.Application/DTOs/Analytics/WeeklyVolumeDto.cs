namespace StrengthPlanner.Application.DTOs.Analytics;

public class WeeklyVolumeDto
{
    public string Muscle { get; set; } = string.Empty;

    /// <summary>
    /// Stimulativne nedeljne serije: doprinos svake serije pomnožen njenom blizinom
    /// otkaza, pa serija daleko od otkaza ne ulazi u zbir.
    /// </summary>
    public decimal Sets { get; set; }

    public int Mev { get; set; }

    /// <summary>
    /// Maksimalni adaptivni volumen — naučena granica mišića, i cilj <b>osnovne</b>
    /// nedelje hipertrofijskog bloka.
    /// </summary>
    public int Mav { get; set; }

    public int Mrv { get; set; }

    /// <summary>
    /// Cilj koji tačno <b>ova</b> nedelja gađa: MAV pomeren onoliko koliko je periodizacija
    /// pomerila propis, i spušten kod bloka snage. Null kada nedelja ne gađa cilj volumena
    /// (deload) ili kada mišić nije u planu te nedelje.
    ///
    /// Postoji zato što je ekran pokazivao MAV kao „cilj" i kada plan nije gadjao MAV —
    /// nedelja volumena cilja iznad njega, nedelja intenziteta ispod.
    /// </summary>
    public decimal? WeekTargetSets { get; set; }

    /// <summary>True kada je izabrana nedelja rasterećenje, pa cilj volumena ne postoji.</summary>
    public bool IsDeloadWeek { get; set; }

    /// <summary>Populaciona seed granica — vrednost na koju reset vraća.</summary>
    public int DefaultMev { get; set; }
    public int DefaultMav { get; set; }
    public int DefaultMrv { get; set; }

    /// <summary>True kada su granice naučene iz korisnikovog odgovora na volumen.</summary>
    public bool IsPersonal { get; set; }

    public string Status { get; set; } = string.Empty;
}
