using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>Propis za jednu nedelju bloka: koliko serija, u kom rep-opsegu i sa kojim RIR-om.</summary>
public sealed record WeekPrescription(
    int WeekNumber,
    bool IsDeload,
    int Sets,
    int RepRangeMin,
    int RepRangeMax,
    int TargetRir);

/// <summary>
/// Raspoređuje propis kroz nedelje bloka.
///
/// Do sada je svaka nedelja mezociklusa nosila isti propis, a jedina razlika je bila
/// deload na kraju. Priručnik na to ima direktan odgovor:
///
/// <i>"Ne možeš isto trenirati svake nedelje i očekivati da napreduješ — telo se
/// prilagodi. Zato se kroz blok menja odnos volumena i intenziteta."</i>
///
/// Tri modela pokrivaju tri načina da se taj odnos menja:
///
/// <list type="bullet">
/// <item><b>Ravan</b> — isti propis svake nedelje. Napredak nosi dupla progresija
/// (ponavljanja pa opterećenje), ne raspored. Četiri nedelje; ovo je ponašanje koje je
/// sistem imao i pre uvođenja modela, pa ostaje podrazumevano.</item>
/// <item><b>Linearan</b> — počinje volumenom (više ponavljanja, lakše serije), završava
/// intenzitetom (manje ponavljanja, bliže otkazu). Klasičan raspored za blok koji vodi ka
/// snazi.</item>
/// <item><b>Obrnut</b> — ista dva kraja, obrnutim redom: teško dok si svež, volumen kada
/// je opterećenje već visoko podiglo zamor. Koristan kada blok vodi ka hipertrofiji.</item>
/// </list>
///
/// Propis se izražava kao <b>pomeraj u odnosu na cilj</b>, ne kao apsolutni broj — isti
/// raspored tako radi i za snagu (3-6 ponavljanja) i za hipertrofiju (8-12), a nivo
/// iskustva i dalje određuje polazni broj serija.
/// </summary>
public static class Periodization
{
    /// <summary>Ravan blok traje četiri nedelje: tri akumulacije i deload.</summary>
    public const int FlatDurationWeeks = 4;

    /// <summary>
    /// Periodizovan blok traje šest nedelja: dve nedelje po fazi, jedna prelazna i deload.
    /// Kraći blok ne bi imao mesta da pomeri odnos volumena i intenziteta.
    /// </summary>
    public const int PeriodizedDurationWeeks = 6;

    /// <summary>Koliko se ponavljanja dodaje u fazi volumena.</summary>
    public const int VolumeRepShift = 3;

    /// <summary>Koliko se ponavljanja oduzima u fazi intenziteta.</summary>
    public const int IntensityRepShift = 2;

    /// <summary>Najmanji broj ponavljanja koji plan sme da propiše.</summary>
    public const int MinReps = 3;

    /// <summary>Najveći broj ponavljanja koji plan sme da propiše.</summary>
    public const int MaxReps = 20;

    /// <summary>Granice ciljnog RIR-a: 0 je otkaz, iznad 4 serija prestaje da stimuliše.</summary>
    public const int MinRir = 0;
    public const int MaxRir = 4;

    /// <summary>Ispod dve serije po vežbi trening prestaje da bude trening.</summary>
    public const int MinSets = 2;

    /// <summary>Pomeraji jedne nedelje u odnosu na propis cilja.</summary>
    private sealed record WeekShape(int RepShift, int RirShift, int SetShift, bool IsDeload = false);

    private static readonly WeekShape Base = new(0, 0, 0);
    private static readonly WeekShape Deload = new(0, 0, 0, IsDeload: true);

    // Ravan blok: tri iste nedelje pa deload — tačno ono što je sistem radio i ranije.
    private static readonly WeekShape[] FlatWeeks = [Base, Base, Base, Deload];

    // Linearan: volumen -> osnova -> intenzitet. RIR pada kroz blok, jer se serije
    // vode sve bliže otkazu kako se volumen povlači.
    private static readonly WeekShape[] LinearWeeks =
    [
        new(VolumeRepShift, 1, 1),
        new(VolumeRepShift, 0, 1),
        Base,
        new(0, -1, 0),
        new(-IntensityRepShift, -1, -1),
        Deload
    ];

    // Obrnut: isti krajevi, obrnutim redom.
    private static readonly WeekShape[] InverseWeeks =
    [
        new(-IntensityRepShift, 1, -1),
        new(-IntensityRepShift, 0, -1),
        Base,
        new(0, -1, 0),
        new(VolumeRepShift, -1, 1),
        Deload
    ];

    private static WeekShape[] ShapesFor(PeriodizationModel model) => model switch
    {
        PeriodizationModel.Linear => LinearWeeks,
        PeriodizationModel.Inverse => InverseWeeks,
        _ => FlatWeeks
    };

    /// <summary>Koliko nedelja blok traje po ovom modelu.</summary>
    public static int DurationWeeks(PeriodizationModel model)
    {
        return ShapesFor(model).Length;
    }

    /// <summary>
    /// Propis za jednu nedelju. <paramref name="weekNumber"/> se broji od 1.
    ///
    /// Deload nedelja zadržava rep-opseg i RIR cilja, a serije se polove — opterećenje
    /// se spušta posebno, na 90% stvarno korišćenog, tek kada se prethodna nedelja završi.
    /// </summary>
    public static WeekPrescription ForWeek(
        PeriodizationModel model,
        int weekNumber,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        var shapes = ShapesFor(model);

        if (weekNumber < 1 || weekNumber > shapes.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                weekNumber,
                $"Week number must fall inside the {shapes.Length}-week block.");
        }

        var shape = shapes[weekNumber - 1];

        if (shape.IsDeload)
        {
            return new WeekPrescription(
                weekNumber,
                IsDeload: true,
                Sets: DeloadSets(baseSets),
                RepRangeMin: baseRepRangeMin,
                RepRangeMax: baseRepRangeMax,
                TargetRir: baseTargetRir);
        }

        // Opseg se pomera kao celina, ali donja granica ne sme ispod tri ponavljanja.
        // Kod snage (3-6) to znači da faza intenziteta ostaje uska — tamo se intenzitet
        // podiže RIR-om, jer niže od tri ponavljanja blok više nije hipertrofija.
        var repRangeMin = Math.Clamp(baseRepRangeMin + shape.RepShift, MinReps, MaxReps - 1);
        var repRangeMax = Math.Clamp(
            baseRepRangeMax + shape.RepShift,
            repRangeMin + 1,
            MaxReps);

        return new WeekPrescription(
            weekNumber,
            IsDeload: false,
            Sets: Math.Max(MinSets, baseSets + shape.SetShift),
            RepRangeMin: repRangeMin,
            RepRangeMax: repRangeMax,
            TargetRir: Math.Clamp(baseTargetRir + shape.RirShift, MinRir, MaxRir));
    }

    /// <summary>Deload polovi serije, ali nikada ispod jedne.</summary>
    public static int DeloadSets(int baseSets)
    {
        return Math.Max(1, (int)Math.Ceiling(baseSets / 2m));
    }

    /// <summary>Ceo blok, nedelja po nedelja.</summary>
    public static IReadOnlyList<WeekPrescription> ForBlock(
        PeriodizationModel model,
        int baseRepRangeMin,
        int baseRepRangeMax,
        int baseTargetRir,
        int baseSets)
    {
        return Enumerable
            .Range(1, DurationWeeks(model))
            .Select(weekNumber => ForWeek(
                model,
                weekNumber,
                baseRepRangeMin,
                baseRepRangeMax,
                baseTargetRir,
                baseSets))
            .ToList();
    }
}
