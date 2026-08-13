using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence;

/// <summary>
/// Ubacuje sistemske seed podatke (mišićne grupe, vežbe, MEV/MRV) pri startu.
/// Idempotentno je: proverava po imenu i ne duplira postojeće.
///
/// Sami podaci žive u <see cref="ExerciseCatalog"/>, uz šablone koji ih referišu —
/// ovde je samo upis u bazu.
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
        var missing = ExerciseCatalog.MuscleGroupNames.Where(name => !existing.Contains(name));

        foreach (var name in missing)
            db.MuscleGroups.Add(new MuscleGroup { Name = name });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExercisesAsync(AppDbContext db, IReadOnlyDictionary<string, Guid> muscleIds)
    {
        // Samo sistemske vežbe smeju da spreče upis seed vežbe. Korisnička vežba istog
        // naziva ne sme, jer bi tada sistemska vežba nedostajala **svim ostalim**
        // korisnicima — a šabloni je traže po nazivu, pa im generisanje plana pada.
        // Poređenje je bez obzira na velika i mala slova, kao i provera naziva pri
        // pravljenju korisničke vežbe.
        var existing = await db.Exercises
            .Where(e => !e.IsCustom)
            .Select(e => e.Name)
            .ToListAsync();
        var existingNames = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in ExerciseCatalog.Exercises.Where(s => !existingNames.Contains(s.Name)))
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
        await ReconcileSystemExercisesAsync(db, muscleIds);
        await AlignSystemExerciseWeightStepsAsync(db);
    }

    /// <summary>
    /// Sistemska vežba mora da odgovara katalogu, ne samo da postoji.
    ///
    /// Upis ubacuje samo vežbe kojih nema, pa bi izmena tipa, sprave ili mišića u
    /// <see cref="ExerciseCatalog"/> ostala samo u kodu — testovi bi je videli, a
    /// zatečena baza ne bi. Korisničke vežbe se ne diraju, kao ni korisnički korak
    /// opterećenja koji živi u UserExerciseSettings.
    /// </summary>
    private static async Task ReconcileSystemExercisesAsync(
        AppDbContext db,
        IReadOnlyDictionary<string, Guid> muscleIds)
    {
        var systemExercises = await db.Exercises
            .Where(exercise => !exercise.IsCustom)
            .Include(exercise => exercise.Muscles)
            .ToListAsync();

        var changed = false;

        foreach (var exercise in systemExercises)
        {
            var seed = ExerciseCatalog.Find(exercise.Name);
            if (seed is null)
            {
                continue;
            }

            if (exercise.Type != seed.Type)
            {
                exercise.Type = seed.Type;
                changed = true;
            }

            if (!string.Equals(exercise.Equipment, seed.Equipment, StringComparison.Ordinal))
            {
                exercise.Equipment = seed.Equipment;
                changed = true;
            }

            var expectedMuscles = seed.Muscles
                .ToDictionary(muscle => muscleIds[muscle.Muscle], muscle => muscle.Contribution);

            var actualMuscles = exercise.Muscles
                .ToDictionary(muscle => muscle.MuscleGroupId, muscle => muscle.Contribution);

            if (expectedMuscles.Count == actualMuscles.Count
                && expectedMuscles.All(expected =>
                    actualMuscles.TryGetValue(expected.Key, out var contribution)
                    && contribution == expected.Value))
            {
                continue;
            }

            exercise.Muscles.Clear();
            foreach (var (muscleGroupId, contribution) in expectedMuscles)
            {
                exercise.Muscles.Add(new ExerciseMuscle
                {
                    MuscleGroupId = muscleGroupId,
                    Contribution = contribution
                });
            }

            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
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
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedVolumeLandmarksAsync(AppDbContext db, IReadOnlyDictionary<string, Guid> muscleIds)
    {
        var existing = await db.VolumeLandmarks.ToDictionaryAsync(v => v.MuscleGroupId);

        foreach (var (muscle, mev, mav, mrv) in ExerciseCatalog.VolumeLandmarks)
        {
            var muscleId = muscleIds[muscle];

            if (existing.TryGetValue(muscleId, out var landmark))
            {
                // Zatečeni redovi nemaju MAV; dopuni ga bez diranja ostalih vrednosti.
                // Vrednost iz priručnika se poravnava na pojas samog reda — ako je neko
                // ručno spustio MRV, cilj ne sme da završi iznad plafona.
                if (landmark.Mav <= 0)
                {
                    landmark.Mav = Math.Clamp(mav, landmark.Mev + 1, landmark.Mrv - 1);
                }

                continue;
            }

            db.VolumeLandmarks.Add(new VolumeLandmark
            {
                MuscleGroupId = muscleId,
                Mev = mev,
                Mav = mav,
                Mrv = mrv
            });
        }

        await db.SaveChangesAsync();
    }
}
