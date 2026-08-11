using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Infrastructure.Persistence;

/// <summary>
/// Ubacuje sistemske seed podatke (mišićne grupe, vežbe, MEV/MRV) pri startu.
/// Idempotentno je: proverava po imenu i ne duplira postojeće.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedMuscleGroupsAsync(db);
        var muscleIds = await db.MuscleGroups.ToDictionaryAsync(m => m.Name, m => m.Id);

        await SeedExercisesAsync(db, muscleIds);
        await SeedVolumeLandmarksAsync(db, muscleIds);
    }

    private static async Task SeedMuscleGroupsAsync(AppDbContext db)
    {
        var existing = await db.MuscleGroups.Select(m => m.Name).ToListAsync();
        var missing = MuscleGroupNames.Where(name => !existing.Contains(name));

        foreach (var name in missing)
            db.MuscleGroups.Add(new MuscleGroup { Name = name });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExercisesAsync(AppDbContext db, IReadOnlyDictionary<string, Guid> muscleIds)
    {
        var existing = await db.Exercises.Select(e => e.Name).ToListAsync();

        foreach (var seed in ExerciseSeeds.Where(s => !existing.Contains(s.Name)))
        {
            var exercise = new Exercise
            {
                Name = seed.Name,
                Type = seed.Type,
                Equipment = seed.Equipment,
                IsCustom = false,
                CreatedByUserId = null,
                WeightStepKg = EquipmentWeightStep.ForEquipment(seed.Equipment),
                Muscles = seed.Muscles
                    .Select(m => new ExerciseMuscle
                    {
                        MuscleGroupId = muscleIds[m.Muscle],
                        Contribution = m.Contribution
                    })
                    .ToList()
            };

            db.Exercises.Add(exercise);
        }

        await db.SaveChangesAsync();
        await AlignSystemExerciseWeightStepsAsync(db);
    }

    /// <summary>
    /// Sistemske vežbe uvek prate korak izveden iz sprave. Postojeći redovi su pre
    /// migracije imali globalnih 2.5 kg, pa se ovde poravnavaju; korisnička odstupanja
    /// žive u UserExerciseSettings i ovim se ne diraju.
    /// </summary>
    private static async Task AlignSystemExerciseWeightStepsAsync(AppDbContext db)
    {
        var systemExercises = await db.Exercises.Where(e => !e.IsCustom).ToListAsync();
        var changed = false;

        foreach (var exercise in systemExercises)
        {
            var expected = EquipmentWeightStep.ForEquipment(exercise.Equipment);
            if (exercise.WeightStepKg != expected)
            {
                exercise.WeightStepKg = expected;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static async Task SeedVolumeLandmarksAsync(AppDbContext db, IReadOnlyDictionary<string, Guid> muscleIds)
    {
        var existing = await db.VolumeLandmarks.Select(v => v.MuscleGroupId).ToListAsync();

        foreach (var (muscle, mev, mrv) in VolumeLandmarkSeeds)
        {
            var muscleId = muscleIds[muscle];
            if (existing.Contains(muscleId))
                continue;

            db.VolumeLandmarks.Add(new VolumeLandmark
            {
                MuscleGroupId = muscleId,
                Mev = mev,
                Mrv = mrv
            });
        }

        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------
    // Seed podaci
    // ---------------------------------------------------------------------

    private static readonly string[] MuscleGroupNames =
    {
        "Chest", "Back", "Shoulders", "Quads", "Hamstrings",
        "Glutes", "Biceps", "Triceps", "Calves", "Abs"
    };

    private record ExerciseSeed(string Name, ExerciseType Type, string Equipment, (string Muscle, decimal Contribution)[] Muscles);

    private static readonly ExerciseSeed[] ExerciseSeeds =
    {
        new("Back Squat", ExerciseType.Compound, "Barbell",
            new[] { ("Quads", 1.0m), ("Glutes", 0.5m), ("Hamstrings", 0.5m) }),
        new("Front Squat", ExerciseType.Compound, "Barbell",
            new[] { ("Quads", 1.0m), ("Glutes", 0.5m) }),
        new("Leg Press", ExerciseType.Compound, "Machine",
            new[] { ("Quads", 1.0m), ("Glutes", 0.5m) }),
        new("Romanian Deadlift", ExerciseType.Compound, "Barbell",
            new[] { ("Hamstrings", 1.0m), ("Glutes", 0.5m), ("Back", 0.5m) }),
        new("Deadlift", ExerciseType.Compound, "Barbell",
            new[] { ("Back", 1.0m), ("Glutes", 0.5m), ("Hamstrings", 0.5m), ("Quads", 0.5m) }),
        new("Leg Curl", ExerciseType.Isolation, "Machine",
            new[] { ("Hamstrings", 1.0m) }),
        new("Leg Extension", ExerciseType.Isolation, "Machine",
            new[] { ("Quads", 1.0m) }),
        new("Hip Thrust", ExerciseType.Compound, "Barbell",
            new[] { ("Glutes", 1.0m), ("Hamstrings", 0.5m) }),
        new("Bench Press", ExerciseType.Compound, "Barbell",
            new[] { ("Chest", 1.0m), ("Triceps", 0.5m), ("Shoulders", 0.5m) }),
        new("Incline Bench Press", ExerciseType.Compound, "Barbell",
            new[] { ("Chest", 1.0m), ("Shoulders", 0.5m), ("Triceps", 0.5m) }),
        new("Dumbbell Bench Press", ExerciseType.Compound, "Dumbbell",
            new[] { ("Chest", 1.0m), ("Triceps", 0.5m), ("Shoulders", 0.5m) }),
        new("Push-up", ExerciseType.Compound, "Bodyweight",
            new[] { ("Chest", 1.0m), ("Triceps", 0.5m) }),
        new("Overhead Press", ExerciseType.Compound, "Barbell",
            new[] { ("Shoulders", 1.0m), ("Triceps", 0.5m) }),
        new("Dumbbell Shoulder Press", ExerciseType.Compound, "Dumbbell",
            new[] { ("Shoulders", 1.0m), ("Triceps", 0.5m) }),
        new("Lateral Raise", ExerciseType.Isolation, "Dumbbell",
            new[] { ("Shoulders", 1.0m) }),
        new("Pull-up", ExerciseType.Compound, "Bodyweight",
            new[] { ("Back", 1.0m), ("Biceps", 0.5m) }),
        new("Lat Pulldown", ExerciseType.Compound, "Machine",
            new[] { ("Back", 1.0m), ("Biceps", 0.5m) }),
        new("Barbell Row", ExerciseType.Compound, "Barbell",
            new[] { ("Back", 1.0m), ("Biceps", 0.5m) }),
        new("Seated Cable Row", ExerciseType.Compound, "Cable",
            new[] { ("Back", 1.0m), ("Biceps", 0.5m) }),
        new("Face Pull", ExerciseType.Isolation, "Cable",
            new[] { ("Shoulders", 1.0m), ("Back", 0.5m) }),
        new("Barbell Curl", ExerciseType.Isolation, "Barbell",
            new[] { ("Biceps", 1.0m) }),
        new("Dumbbell Curl", ExerciseType.Isolation, "Dumbbell",
            new[] { ("Biceps", 1.0m) }),
        new("Triceps Pushdown", ExerciseType.Isolation, "Cable",
            new[] { ("Triceps", 1.0m) }),
        new("Overhead Triceps Extension", ExerciseType.Isolation, "Dumbbell",
            new[] { ("Triceps", 1.0m) }),
        new("Calf Raise", ExerciseType.Isolation, "Machine",
            new[] { ("Calves", 1.0m) }),
        new("Plank", ExerciseType.Isolation, "Bodyweight",
            new[] { ("Abs", 1.0m) }),
        new("Cable Crunch", ExerciseType.Isolation, "Cable",
            new[] { ("Abs", 1.0m) })
    };

    // (Mišićna grupa, MEV, MRV) — orijentacione nedeljne radne serije.
    private static readonly (string Muscle, int Mev, int Mrv)[] VolumeLandmarkSeeds =
    {
        ("Chest", 10, 22),
        ("Back", 10, 25),
        ("Shoulders", 8, 26),
        ("Quads", 8, 20),
        ("Hamstrings", 6, 16),
        ("Glutes", 4, 16),
        ("Biceps", 8, 20),
        ("Triceps", 6, 18),
        ("Calves", 8, 20),
        ("Abs", 6, 25)
    };
}
