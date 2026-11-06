using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Zamena vežbe za ostatak bloka (A2) menja samo vežbu, a propis ostaje. Zato zamena mora biti
/// vežba kojoj bi blok propisao isto.
/// </summary>
public class ExerciseSubstitutionTests
{
    [Fact]
    public void ACompound_IsReplacedByACompoundOfTheSameMuscleAndRange()
    {
        Assert.True(Can("Back Squat", "Front Squat"));
        Assert.True(Can("Bench Press", "Dumbbell Bench Press"));
        Assert.True(Can("Barbell Row", "Seated Cable Row"));
    }

    /// <summary>
    /// Iskorak ne podnosi opseg snage (runda 14): na mestu čučnja bi u bloku snage nasledio 3-6.
    /// </summary>
    [Fact]
    public void ACompoundThatDoesNotSuitLowReps_DoesNotTakeTheSlotOfOneThatDoes()
    {
        Assert.False(Can("Back Squat", "Bulgarian Split Squat"));
        Assert.False(Can("Bulgarian Split Squat", "Back Squat"));
        Assert.True(Can("Bulgarian Split Squat", "Walking Lunge"));
    }

    [Fact]
    public void AnIsolation_IsReplacedByAnIsolationOfTheSameMuscle()
    {
        Assert.True(Can("Cable Fly", "Dumbbell Fly"));
        Assert.True(Can("Barbell Curl", "Hammer Curl"));
        Assert.False(Can("Cable Fly", "Bench Press"));
        Assert.False(Can("Bench Press", "Cable Fly"));
    }

    [Fact]
    public void AnotherMuscle_IsNeverOffered()
    {
        // Glave ramena su tri grupe (#109): bočno podizanje ne zamenjuje rad za zadnji deltoid.
        Assert.False(Can("Lateral Raise", "Rear Delt Fly"));
        Assert.False(Can("Leg Curl", "Leg Extension"));
    }

    [Fact]
    public void AHoldWithoutLoad_IsNeverOffered()
    {
        Assert.False(Can("Cable Crunch", "Plank"));
        Assert.True(Can("Cable Crunch", "Machine Crunch"));
    }

    /// <summary>
    /// Lična vežba sme da ima više glavnih mišića. Poklapa se ceo skup, pa ishod ne zavisi od
    /// toga kojim redom baza vrati udele (pregled #111).
    /// </summary>
    [Fact]
    public void ALiftersOwnExerciseWithTwoPrimaries_MatchesOnlyTheSameTwo()
    {
        var chest = Guid.NewGuid();
        var triceps = Guid.NewGuid();
        var bench = new SubstitutionProfile(Guid.NewGuid(), ExerciseType.Compound, true, [chest], true);
        var custom = new SubstitutionProfile(Guid.NewGuid(), ExerciseType.Compound, true, [triceps, chest], true);
        var sameTwo = new SubstitutionProfile(Guid.NewGuid(), ExerciseType.Compound, true, [chest, triceps], true);

        Assert.False(ExerciseSubstitution.CanReplace(bench, custom));
        Assert.False(ExerciseSubstitution.CanReplace(custom, bench));
        Assert.True(ExerciseSubstitution.CanReplace(custom, sameTwo));
    }

    [Fact]
    public void AnExercise_DoesNotReplaceItself()
    {
        Assert.False(Can("Back Squat", "Back Squat"));
    }

    private static bool Can(string current, string candidate)
    {
        return ExerciseSubstitution.CanReplace(Profile(current), Profile(candidate));
    }

    private static SubstitutionProfile Profile(string name)
    {
        var seed = ExerciseCatalog.Find(name)!;
        var primary = seed.Muscles.Where(muscle => muscle.Contribution == 1.0m).Select(muscle => muscle.Muscle).Single();

        return new SubstitutionProfile(
            TemplateWeekSimulation.MuscleId(name),
            seed.Type,
            seed.SuitsLowReps,
            [TemplateWeekSimulation.MuscleId(primary)],
            !(seed.Equipment == "Bodyweight" && seed.BodyweightShare == 0m));
    }
}
