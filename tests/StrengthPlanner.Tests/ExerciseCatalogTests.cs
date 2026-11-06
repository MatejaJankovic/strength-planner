using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Invarijante kataloga koje šabloni tiho pretpostavljaju umesto da ih provere:
/// <see cref="WorkoutTemplateCatalog"/>'s testovi proveravaju samo vežbe koje neki šablon
/// zaista koristi, pa vežba sa lošim podatkom koji nijedan šablon (još) ne koristi prolazi
/// neopaženo dok se ne doda u neki šablon.
/// </summary>
public class ExerciseCatalogTests
{
    [Fact]
    public void EveryExercise_HasExactlyOnePrimaryMuscle()
    {
        // ExerciseService.ValidateMuscles prihvata bilo koji broj doprinosa od 1.0 —
        // ovaj test čuva konvenciju koju sistemske vežbe same nameću sebi: tačno jedan
        // primarni mišić, ostali sekundarni. Predlog šablona iz istraživanja je prvobitno
        // nudio dva primarna mišića za dve nove vežbe; ovaj test bi to uhvatio.
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            var primaryCount = exercise.Muscles.Count(muscle => muscle.Contribution == 1.0m);

            Assert.True(
                primaryCount == 1,
                $"{exercise.Name}: {primaryCount} primarnih mišića umesto tačno jednog.");
        }
    }

    [Fact]
    public void EveryExercise_OnlyUsesThePrimaryOrSecondaryContributionScale()
    {
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            foreach (var muscle in exercise.Muscles)
            {
                Assert.True(
                    muscle.Contribution is 1.0m or 0.5m,
                    $"{exercise.Name}/{muscle.Muscle}: doprinos {muscle.Contribution} nije 1.0 ni 0.5.");
            }
        }
    }

    [Fact]
    public void EveryExercise_OnlyReferencesTrackedMuscleGroups()
    {
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            foreach (var muscle in exercise.Muscles)
            {
                Assert.Contains(muscle.Muscle, ExerciseCatalog.MuscleGroupNames);
            }
        }
    }

    [Fact]
    public void EveryExercise_HasNoDuplicateMuscleEntries()
    {
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            var names = exercise.Muscles.Select(muscle => muscle.Muscle).ToList();

            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void EveryExercise_UsesARecognizedEquipmentType()
    {
        // EquipmentWeightStep.ForEquipment se tiho vraća na generički korak za
        // neprepoznatu opremu umesto da baci grešku — pravopisna greška bi prošla nemo i
        // vežba bi dobila pogrešan korak opterećenja. Provera ide na
        // EquipmentWeightStep.RecognizedEquipment, ne na sopstveni spisak: dva spiska iste
        // opreme bi se razišla prvi put kad neko doda spravu na jednom mestu a zaboravi
        // drugo.
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            Assert.Contains(
                exercise.Equipment,
                EquipmentWeightStep.RecognizedEquipment,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryExerciseName_IsUnique()
    {
        var names = ExerciseCatalog.Exercises.Select(exercise => exercise.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Migracija SplitShoulderHeads prepisuje naučene granice zbirne grupe "Shoulders" na
    /// bočni deltoid bez preračuna. To je tačno samo dok bočni deltoid nosi iste seed
    /// vrednosti koje je imala zbirna grupa (8/16/26): naučena granica je pomak od seed-a, i
    /// sa drugim seed-om bi isti broj značio nešto drugo.
    /// </summary>
    [Fact]
    public void SideDelts_KeepTheSeedTheShoulderGroupHad()
    {
        var sideDelts = ExerciseCatalog.VolumeLandmarks.Single(seed => seed.Muscle == ExerciseCatalog.SideDelts);

        Assert.Equal((8, 16, 26), (sideDelts.Mev, sideDelts.Mav, sideDelts.Mrv));
    }

    /// <summary>
    /// MEV prednjeg i zadnjeg deltoida je nula zato što ih složene vežbe već stimulišu, a model
    /// te serije glavama namerno ne pripisuje. Kad bi neka horizontalna potisna ili veslačka
    /// vežba dobila udeo u tim glavama, ista stimulacija bi se brojala dva puta: jednom u nuli
    /// MEV-a, jednom u budžetu, iz kog bi balansiranje onda skidalo direktan rad.
    /// </summary>
    [Fact]
    public void OnlyDirectWork_CountsTowardTheFrontAndRearDelts()
    {
        var credited = ExerciseCatalog.Exercises
            .Where(exercise => exercise.Muscles.Any(muscle =>
                muscle.Muscle is ExerciseCatalog.FrontDelts or ExerciseCatalog.RearDelts))
            .Select(exercise => exercise.Name)
            .Order()
            .ToList();

        Assert.Equal(
            new[] { "Dumbbell Shoulder Press", "Face Pull", "Overhead Press", "Rear Delt Fly" }.Order(),
            credited);
    }
}
