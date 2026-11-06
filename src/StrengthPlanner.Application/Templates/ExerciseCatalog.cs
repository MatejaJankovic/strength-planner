using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.Templates;

/// <summary>Doprinos jedne vežbe jednoj mišićnoj grupi: 1.0 primarna, 0.5 sekundarna.</summary>
public sealed record MuscleContributionSeed(string Muscle, decimal Contribution);

/// <summary>Sistemska vežba: naziv, tip, sprava, mišići i deo telesne mase koji podiže.</summary>
/// <param name="Name">Naziv vežbe.</param>
/// <param name="Type">Složena ili izolaciona.</param>
/// <param name="Equipment">Sprava; odatle izlazi i korak opterećenja.</param>
/// <param name="Muscles">Mišićne grupe i njihovi doprinosi.</param>
/// <param name="BodyweightShare">
/// Koliki deo telesne mase vežba podiže. Nula za sve sa spoljnim opterećenjem. Procene za
/// vežbe sa telesnom masom su namerno grube: greška od 10% u udelu pomera opterećenje za
/// manje od jednog koraka tega.
/// </param>
/// <param name="SuitsLowReps">
/// Da li složena vežba podnosi opseg snage (3–6). Ne podnose ga vežbe na jednoj nozi, one sa
/// nestabilnim uslovima i one kojima se opterećenje ne može dodati — u bloku snage one ostaju
/// pomoćni rad na 8–12. Za izolacije nije bitno: one su uvek pomoćni rad.
/// </param>
public sealed record ExerciseSeed(
    string Name,
    ExerciseType Type,
    string Equipment,
    IReadOnlyList<MuscleContributionSeed> Muscles,
    decimal BodyweightShare = 0m,
    bool SuitsLowReps = true);

/// <summary>Orijentacione nedeljne radne serije po mišićnoj grupi.</summary>
public sealed record VolumeLandmarkSeed(string Muscle, int Mev, int Mav, int Mrv);

/// <summary>
/// Ugrađene vežbe i granice volumena.
///
/// Stoje uz <see cref="WorkoutTemplateCatalog"/> jer šabloni referišu vežbe po nazivu i
/// zavise od njihovog tipa (složena / izolaciona) i mišića koje pogađaju. Dok je ovaj
/// spisak živeo u sloju baze, nijedan test nije mogao da proveri da li šabloni pogađaju
/// postojeće vežbe niti koliko volumena zaista propisuju.
/// </summary>
public static class ExerciseCatalog
{
    // Rame je dugo bilo jedna zbirna grupa, "Shoulders", sa jednim ciljem za tri glave koje
    // rade različite vežbe. Taj cilj su punili potisak iznad glave i rad za zadnji deltoid, pa
    // je grupa stajala u zoni dok je bočni deltoid, koji gradi samo bočno podizanje, dobijao
    // 6-9 serija nedeljno (docs/features/shoulder-heads.md).
    public const string FrontDelts = "Front Delts";
    public const string SideDelts = "Side Delts";
    public const string RearDelts = "Rear Delts";

    public static readonly IReadOnlyList<string> MuscleGroupNames =
    [
        "Chest", "Back", FrontDelts, SideDelts, RearDelts, "Quads", "Hamstrings",
        "Glutes", "Biceps", "Triceps", "Calves", "Abs"
    ];

    public static readonly IReadOnlyList<ExerciseSeed> Exercises =
    [
        // Čučanj ne nosi zadnju ložu. Ona u čučnju radi izometrijski — drži koleno i kuk
        // u odnosu, a ne skraćuje se pod opterećenjem — pa serija čučnja nije pola serije
        // za zadnju ložu ma koliko teška bila. Katalog je to i sam govorio na dva mesta:
        // Front Squat i Split Squat je nikada nisu ni imali, a Back Squat i Bulgarian
        // Split Squat, isti obrazac pokreta, jesu.
        //
        // Posledica koju je taj kredit imao nije bila kozmetička: u šablonu Upper/Lower x3
        // je POLOVINA nedeljnog volumena zadnje lože dolazila od čučnjeva i iskoraka
        // (8 od 16, uz MAV 11), pa je balansiranje videlo mišić preko cilja i skidalo
        // serije sa pregiba — jedine vežbe koja ga zaista gradi.
        new("Back Squat", ExerciseType.Compound, "Barbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Front Squat", ExerciseType.Compound, "Barbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Leg Press", ExerciseType.Compound, "Machine",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Romanian Deadlift", ExerciseType.Compound, "Barbell",
            [new("Hamstrings", 1.0m), new("Glutes", 0.5m), new("Back", 0.5m)]),
        // Mrtvo dizanje je opružanje kuka, i pokret nosi gluteus. Zadnja loža dobija pola, a
        // ne pun udeo kao u rumunskom mrtvom dizanju: koleno se opruža zajedno sa kukom, pa
        // se taj dvozglobni mišić malo menja po dužini. Leđa zadržavaju pola: opružači rade
        // izometrijski, ali pod velikom napetošću, a za razliku od zadnje lože u čučnju tu
        // napetost i nose. Dok je Back bio 1.0, Upper/Lower x3 je dostizao MEV za leđa samo
        // kroz mrtvo dizanje (F16).
        new("Deadlift", ExerciseType.Compound, "Barbell",
            [new("Glutes", 1.0m), new("Hamstrings", 0.5m), new("Back", 0.5m), new("Quads", 0.5m)]),
        // Zadnja loža u potisku kukom radi uz savijeno koleno, skraćena i sa malo opterećenja;
        // gluteus nosi pokret. Udeo od 0.5 je punio budžet zadnje lože koji pregib gradi.
        new("Hip Thrust", ExerciseType.Compound, "Barbell",
            [new("Glutes", 1.0m)]),
        new("Leg Curl", ExerciseType.Isolation, "Machine",
            [new("Hamstrings", 1.0m)]),
        new("Leg Extension", ExerciseType.Isolation, "Machine",
            [new("Quads", 1.0m)]),
        new("Calf Raise", ExerciseType.Isolation, "Machine",
            [new("Calves", 1.0m)]),

        // Do ovde svaka vežba za noge tražila je šipku ili mašinu. Šest novih pokriva i
        // bučice i telesnu težinu, i dodaju jedini unilateralni obrazac u katalogu — dosad
        // nijedna vežba nije izolovala jednu nogu.
        new("Bulgarian Split Squat", ExerciseType.Compound, "Dumbbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)], SuitsLowReps: false),
        // Iskorak na jednoj nozi nosi telo bez oslonjene potkolenice i stopala.
        new("Split Squat", ExerciseType.Compound, "Bodyweight",
            [new("Quads", 1.0m), new("Glutes", 0.5m)], BodyweightShare: 0.85m, SuitsLowReps: false),
        new("Walking Lunge", ExerciseType.Compound, "Dumbbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)], SuitsLowReps: false),
        new("Goblet Squat", ExerciseType.Compound, "Dumbbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)], SuitsLowReps: false),
        new("Step-Up", ExerciseType.Compound, "Dumbbell",
            [new("Glutes", 1.0m), new("Quads", 0.5m)], SuitsLowReps: false),
        new("Single-Leg Romanian Deadlift", ExerciseType.Compound, "Dumbbell",
            [new("Hamstrings", 1.0m), new("Glutes", 0.5m)], SuitsLowReps: false),

        // Horizontalni potisak ne ulazi ni u budžet prednjeg deltoida. Prednji deltoid u njemu
        // radi i raste, ali granice za njega (RP) važe za direktan rad POVRH potisaka za grudi:
        // MEV mu je nula upravo zato što potisak već daje stimulus. Da potisak puni i budžet,
        // MRV od 10 serija bi prešla sama nedelja grudi.
        //
        // Push-up je isti obrazac pokreta i nikada nije imao udeo u ramenima. Vertikalni
        // potisci (Overhead Press, Dumbbell Shoulder Press) su direktan rad za prednji deltoid,
        // a bočni opterećuju upola.
        new("Bench Press", ExerciseType.Compound, "Barbell",
            [new("Chest", 1.0m), new("Triceps", 0.5m)]),
        new("Incline Bench Press", ExerciseType.Compound, "Barbell",
            [new("Chest", 1.0m), new("Triceps", 0.5m)]),
        new("Dumbbell Bench Press", ExerciseType.Compound, "Dumbbell",
            [new("Chest", 1.0m), new("Triceps", 0.5m)]),
        // Sklek u gornjem položaju nosi oko dve trećine telesne mase.
        new("Push-up", ExerciseType.Compound, "Bodyweight",
            [new("Chest", 1.0m), new("Triceps", 0.5m)], BodyweightShare: 0.64m, SuitsLowReps: false),

        // Grudi i leđa dugo nisu imali nijednu izolacionu vežbu. Naprednom vežbaču
        // pripada jedna složena vežba po treningu, pa se te grupe nije imalo čime dopuniti.
        new("Cable Fly", ExerciseType.Isolation, "Cable",
            [new("Chest", 1.0m)]),
        new("Dumbbell Fly", ExerciseType.Isolation, "Dumbbell",
            [new("Chest", 1.0m)]),

        new("Overhead Press", ExerciseType.Compound, "Barbell",
            [new(FrontDelts, 1.0m), new(SideDelts, 0.5m), new("Triceps", 0.5m)]),
        new("Dumbbell Shoulder Press", ExerciseType.Compound, "Dumbbell",
            [new(FrontDelts, 1.0m), new(SideDelts, 0.5m), new("Triceps", 0.5m)]),
        new("Lateral Raise", ExerciseType.Isolation, "Dumbbell",
            [new(SideDelts, 1.0m)]),
        new("Rear Delt Fly", ExerciseType.Isolation, "Dumbbell",
            [new(RearDelts, 1.0m)]),
        new("Face Pull", ExerciseType.Isolation, "Cable",
            [new(RearDelts, 1.0m), new("Back", 0.5m)]),

        // Veslanje ne dobija udeo u zadnjem deltoidu, iako on u njemu radi dinamički. Granice
        // za zadnji deltoid (RP) važe za direktan rad povrh rada za leđa, i MEV mu je nula
        // upravo zato što veslanje daje stimulus; da veslanje puni i budžet, balansiranje bi
        // skidalo serije sa jedine direktne vežbe. Dok je rame bilo zbirna grupa, isti kredit
        // je skidao serije sa bočnog podizanja (F16, docs/features/muscle-contributions-2.md).
        //
        // Zgib podiže celo telo.
        new("Pull-up", ExerciseType.Compound, "Bodyweight",
            [new("Back", 1.0m), new("Biceps", 0.5m)], BodyweightShare: 1.00m),
        new("Lat Pulldown", ExerciseType.Compound, "Machine",
            [new("Back", 1.0m), new("Biceps", 0.5m)]),
        new("Barbell Row", ExerciseType.Compound, "Barbell",
            [new("Back", 1.0m), new("Biceps", 0.5m)]),
        new("Seated Cable Row", ExerciseType.Compound, "Cable",
            [new("Back", 1.0m), new("Biceps", 0.5m)]),
        new("Straight-Arm Pulldown", ExerciseType.Isolation, "Cable",
            [new("Back", 1.0m)]),

        new("Barbell Curl", ExerciseType.Isolation, "Barbell",
            [new("Biceps", 1.0m)]),
        new("Dumbbell Curl", ExerciseType.Isolation, "Dumbbell",
            [new("Biceps", 1.0m)]),
        new("Hammer Curl", ExerciseType.Isolation, "Dumbbell",
            [new("Biceps", 1.0m)]),
        new("Triceps Pushdown", ExerciseType.Isolation, "Cable",
            [new("Triceps", 1.0m)]),
        new("Overhead Triceps Extension", ExerciseType.Isolation, "Dumbbell",
            [new("Triceps", 1.0m)]),
        new("Skull Crusher", ExerciseType.Isolation, "Barbell",
            [new("Triceps", 1.0m)]),

        // Plank je izdržaj, a ne ponavljanja: ostaje u katalogu zbog starih planova i ličnih
        // šablona, ali ga ugrađeni šabloni više ne koriste (vidi WorkoutTemplateCatalog).
        // Udeo telesne mase mu je 0, pa ne dobija ni procenu maksimuma ni predlog težine.
        new("Plank", ExerciseType.Isolation, "Bodyweight",
            [new("Abs", 1.0m)]),
        new("Cable Crunch", ExerciseType.Isolation, "Cable",
            [new("Abs", 1.0m)]),
        // Zamena za plank u ugrađenim šablonima: isti tip i isti mišić, ali se opterećuje,
        // pa i napreduje kao svaka druga vežba.
        new("Machine Crunch", ExerciseType.Isolation, "Machine",
            [new("Abs", 1.0m)])
    ];

    // MAV je ciljna vrednost; priručnik je smešta u raspon 8-20 serija nedeljno.
    //
    // Priručnik ne deli rame na glave, pa su granice glava RP procene za srednji nivo
    // (rpstrength.com, članci o prednjem, bočnom i zadnjem deltoidu), date kao rasponi.
    // MAV i MRV su sredina raspona. MEV prednjeg i zadnjeg deltoida je nula, kao u RP: prvi
    // dobija stimulus od potisaka za grudi, drugi od veslanja, a model te serije glavama
    // namerno ne pripisuje (vidi komentare uz potiske i veslanja), pa direktan rad za njih
    // nije minimum nego dodatak. Bočni deltoid od složenih vežbi dobija samo pola potiska
    // iznad glave; on zadržava stare vrednosti zbirne grupe, koje su u njegovim rasponima
    // (MEV 6-8, MAV 8-24, MRV 24-30), pa se naučene granice ramena na njega prenose bez
    // preračuna.
    public static readonly IReadOnlyList<VolumeLandmarkSeed> VolumeLandmarks =
    [
        new("Chest", 10, 16, 22),
        new("Back", 10, 18, 25),
        new(FrontDelts, 0, 6, 10),
        new(SideDelts, 8, 16, 26),
        new(RearDelts, 0, 8, 16),
        new("Quads", 8, 14, 20),
        new("Hamstrings", 6, 11, 16),
        new("Glutes", 4, 10, 16),
        new("Biceps", 8, 14, 20),
        new("Triceps", 6, 12, 18),
        new("Calves", 8, 13, 20),
        new("Abs", 6, 12, 25)
    ];

    private static readonly IReadOnlyDictionary<string, ExerciseSeed> ByName =
        Exercises.ToDictionary(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase);

    public static ExerciseSeed? Find(string name)
    {
        return ByName.GetValueOrDefault(name);
    }

    public static bool IsCompound(string name)
    {
        return Find(name)?.Type == ExerciseType.Compound;
    }
}
