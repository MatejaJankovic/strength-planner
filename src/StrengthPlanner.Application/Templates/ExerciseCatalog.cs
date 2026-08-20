using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.Templates;

/// <summary>Doprinos jedne vežbe jednoj mišićnoj grupi: 1.0 primarna, 0.5 sekundarna.</summary>
public sealed record MuscleContributionSeed(string Muscle, decimal Contribution);

/// <summary>Sistemska vežba: naziv, tip, sprava i mišići koje pogađa.</summary>
public sealed record ExerciseSeed(
    string Name,
    ExerciseType Type,
    string Equipment,
    IReadOnlyList<MuscleContributionSeed> Muscles);

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
    public static readonly IReadOnlyList<string> MuscleGroupNames =
    [
        "Chest", "Back", "Shoulders", "Quads", "Hamstrings",
        "Glutes", "Biceps", "Triceps", "Calves", "Abs"
    ];

    public static readonly IReadOnlyList<ExerciseSeed> Exercises =
    [
        new("Back Squat", ExerciseType.Compound, "Barbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m), new("Hamstrings", 0.5m)]),
        new("Front Squat", ExerciseType.Compound, "Barbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Leg Press", ExerciseType.Compound, "Machine",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Romanian Deadlift", ExerciseType.Compound, "Barbell",
            [new("Hamstrings", 1.0m), new("Glutes", 0.5m), new("Back", 0.5m)]),
        new("Deadlift", ExerciseType.Compound, "Barbell",
            [new("Back", 1.0m), new("Glutes", 0.5m), new("Hamstrings", 0.5m), new("Quads", 0.5m)]),
        new("Hip Thrust", ExerciseType.Compound, "Barbell",
            [new("Glutes", 1.0m), new("Hamstrings", 0.5m)]),
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
            [new("Quads", 1.0m), new("Glutes", 0.5m), new("Hamstrings", 0.5m)]),
        new("Split Squat", ExerciseType.Compound, "Bodyweight",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Walking Lunge", ExerciseType.Compound, "Dumbbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m), new("Hamstrings", 0.5m)]),
        new("Goblet Squat", ExerciseType.Compound, "Dumbbell",
            [new("Quads", 1.0m), new("Glutes", 0.5m)]),
        new("Step-Up", ExerciseType.Compound, "Dumbbell",
            [new("Glutes", 1.0m), new("Quads", 0.5m), new("Hamstrings", 0.5m)]),
        new("Single-Leg Romanian Deadlift", ExerciseType.Compound, "Dumbbell",
            [new("Hamstrings", 1.0m), new("Glutes", 0.5m)]),

        new("Bench Press", ExerciseType.Compound, "Barbell",
            [new("Chest", 1.0m), new("Triceps", 0.5m), new("Shoulders", 0.5m)]),
        new("Incline Bench Press", ExerciseType.Compound, "Barbell",
            [new("Chest", 1.0m), new("Shoulders", 0.5m), new("Triceps", 0.5m)]),
        new("Dumbbell Bench Press", ExerciseType.Compound, "Dumbbell",
            [new("Chest", 1.0m), new("Triceps", 0.5m), new("Shoulders", 0.5m)]),
        new("Push-up", ExerciseType.Compound, "Bodyweight",
            [new("Chest", 1.0m), new("Triceps", 0.5m)]),

        // Grudi i leđa dugo nisu imali nijednu izolacionu vežbu. Naprednom vežbaču
        // pripada jedna složena vežba po treningu, pa se te grupe nije imalo čime dopuniti.
        new("Cable Fly", ExerciseType.Isolation, "Cable",
            [new("Chest", 1.0m)]),
        new("Dumbbell Fly", ExerciseType.Isolation, "Dumbbell",
            [new("Chest", 1.0m)]),

        new("Overhead Press", ExerciseType.Compound, "Barbell",
            [new("Shoulders", 1.0m), new("Triceps", 0.5m)]),
        new("Dumbbell Shoulder Press", ExerciseType.Compound, "Dumbbell",
            [new("Shoulders", 1.0m), new("Triceps", 0.5m)]),
        new("Lateral Raise", ExerciseType.Isolation, "Dumbbell",
            [new("Shoulders", 1.0m)]),
        new("Rear Delt Fly", ExerciseType.Isolation, "Dumbbell",
            [new("Shoulders", 1.0m)]),
        new("Face Pull", ExerciseType.Isolation, "Cable",
            [new("Shoulders", 1.0m), new("Back", 0.5m)]),

        new("Pull-up", ExerciseType.Compound, "Bodyweight",
            [new("Back", 1.0m), new("Biceps", 0.5m)]),
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

        new("Plank", ExerciseType.Isolation, "Bodyweight",
            [new("Abs", 1.0m)]),
        new("Cable Crunch", ExerciseType.Isolation, "Cable",
            [new("Abs", 1.0m)])
    ];

    // MAV je ciljna vrednost; priručnik je smešta u raspon 8-20 serija nedeljno.
    public static readonly IReadOnlyList<VolumeLandmarkSeed> VolumeLandmarks =
    [
        new("Chest", 10, 16, 22),
        new("Back", 10, 18, 25),
        new("Shoulders", 8, 16, 26),
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
