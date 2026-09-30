using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Cilj bloka opisuje kako vežbač namerava da ojača, a snaga se izražava u pokretima koji
/// mogu da nose opterećenje. Izolacija ne može: tri ponavljanja bočnog podizanja nisu
/// provera sile nego način da se rame optereti težinom za koju mišić nikada nije bio
/// ograničavajući faktor.
///
/// Do ove izmene je svaka vežba u ugrađenom šablonu dobijala opseg cilja, pa je blok snage
/// propisivao 3–6 za bočno podizanje, letenje, biceps pregib i triceps ekstenziju — a u
/// petoj nedelji linearnog modela 3–4. Napredan vežbač u bloku snage ima jednu složenu i
/// pet izolacija, dakle pet od šest vežbi je nosilo opseg koji im ne pripada.
/// </summary>
public class GoalPrescriptionTests
{
    [Fact]
    public void AStrengthBlock_KeepsIsolationInTheHypertrophyRange()
    {
        var compound = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Compound, suitsLowReps: true);
        var isolation = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Isolation, suitsLowReps: true);

        Assert.Equal(3, compound.RepRangeMin);
        Assert.Equal(6, compound.RepRangeMax);

        Assert.Equal(8, isolation.RepRangeMin);
        Assert.Equal(12, isolation.RepRangeMax);
    }

    /// <summary>
    /// Rezerva je koliko nedelja treba da bude teška, a to je svojstvo nedelje, a ne
    /// pokreta — pa ciljni RIR i za izolaciju ostaje onaj bloka. To je i ono što dozvoljava
    /// da deload i vraćanje oslobođene nedelje rade sa jednim brojem po bloku.
    /// </summary>
    [Fact]
    public void TheTargetRir_FollowsTheBlock_NotTheExercise()
    {
        foreach (var goal in Enum.GetValues<Goal>())
        {
            var blockRir = GoalPrescriptions.ForGoal(goal).TargetRir;

            Assert.Equal(blockRir, GoalPrescriptions.ForExercise(goal, ExerciseType.Compound, suitsLowReps: true).TargetRir);
            Assert.Equal(blockRir, GoalPrescriptions.ForExercise(goal, ExerciseType.Isolation, suitsLowReps: true).TargetRir);
        }
    }

    [Fact]
    public void AHypertrophyBlock_IsUnchangedForBothKinds()
    {
        var goal = GoalPrescriptions.ForGoal(Goal.Hypertrophy);

        foreach (var type in Enum.GetValues<ExerciseType>())
        {
            Assert.Equal(goal, GoalPrescriptions.ForExercise(Goal.Hypertrophy, type, suitsLowReps: true));
        }
    }

    /// <summary>
    /// Periodizacija se primenjuje na oba opsega kao i na svaki drugi, pa se razlika vidi
    /// kroz ceo blok — ne samo u prvoj nedelji.
    /// </summary>
    [Fact]
    public void TheDifferenceSurvivesPeriodization()
    {
        var compound = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Compound, suitsLowReps: true);
        var isolation = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Isolation, suitsLowReps: true);

        static string Window(GoalPrescription prescription, int week)
        {
            var result = Periodization.ForWeek(
                PeriodizationModel.Linear,
                week,
                prescription.RepRangeMin,
                prescription.RepRangeMax,
                prescription.TargetRir,
                4);

            return $"{result.RepRangeMin}-{result.RepRangeMax}";
        }

        // Nedelja intenziteta: složena vežba ide na trojku, izolacija ostaje u opsegu
        // u kome izolacija i ima smisla.
        Assert.Equal("3-4", Window(compound, 5));
        Assert.Equal("6-10", Window(isolation, 5));

        // Deload vraća osnovni opseg svake vežbe, pa i tu ostaju razdvojeni.
        Assert.Equal("3-6", Window(compound, 6));
        Assert.Equal("8-12", Window(isolation, 6));
    }

    /// <summary>
    /// Opseg odlučuje i o polaznom opterećenju, pa izolacija sa poznatim maksimumom u bloku
    /// snage od sada startuje lakše — što je i smisao izmene. Za bočno podizanje sa
    /// maksimumom od 40 kg: propis 8–12 pri RIR 2 traži deset efektivnih ponavljanja, a
    /// 3–6 samo pet.
    /// </summary>
    [Fact]
    public void TheRangeAlsoDecidesTheStartingLoad()
    {
        var calculator = new E1RmCalculator();
        var strength = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Compound, suitsLowReps: true);
        var isolation = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Isolation, suitsLowReps: true);

        var onStrengthRange = calculator.WorkingWeightFor(
            oneRepMax: 40m,
            targetReps: strength.RepRangeMin,
            targetRir: strength.TargetRir,
            weightStepKg: 2.5m);
        var onIsolationRange = calculator.WorkingWeightFor(
            oneRepMax: 40m,
            targetReps: isolation.RepRangeMin,
            targetRir: isolation.TargetRir,
            weightStepKg: 2.5m);

        Assert.Equal(35m, onStrengthRange);
        Assert.Equal(30m, onIsolationRange);
    }

    /// <summary>
    /// Ishod koji plan treba da dobije, izračunat iz kataloga i pravila: u bloku snage opseg
    /// snage nosi tačno svaka složena vežba koja ga podnosi, a nijedna izolacija i nijedna
    /// vežba na jednoj nozi ili bez načina da se doda teret. Vezivanje ovog pravila za
    /// generator dokazuje E2E, jer servisi nemaju test harness — ovo drži ishod, ne prolaz
    /// kroz kod.
    /// </summary>
    [Fact]
    public void InAStrengthBlock_OnlyLiftsThatSuitLowRepsSitOnTheStrengthRange()
    {
        var strengthRange = GoalPrescriptions.ForGoal(Goal.Strength);
        var places = 0;

        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            foreach (var day in template.Days)
            {
                foreach (var name in day.Exercises)
                {
                    var exercise = ExerciseCatalog.Find(name);
                    if (exercise is null)
                    {
                        continue;
                    }

                    var prescription = GoalPrescriptions.ForExercise(Goal.Strength, exercise.Type, exercise.SuitsLowReps);
                    places++;

                    if (exercise.Type == ExerciseType.Compound && exercise.SuitsLowReps)
                    {
                        Assert.Equal(strengthRange.RepRangeMin, prescription.RepRangeMin);
                        Assert.Equal(strengthRange.RepRangeMax, prescription.RepRangeMax);
                    }
                    else
                    {
                        Assert.NotEqual(strengthRange.RepRangeMin, prescription.RepRangeMin);
                        Assert.NotEqual(strengthRange.RepRangeMax, prescription.RepRangeMax);
                    }
                }
            }
        }

        Assert.True(places > 50, $"Provereno samo {places} mesta u šablonima.");
    }

    /// <summary>
    /// Pravilo vredi samo ako katalog vežbe zaista razvrstava. Ovo su vežbe koje je blok
    /// snage propisivao na 3–6 pre izmene.
    /// </summary>
    [Theory]
    [InlineData("Lateral Raise")]
    [InlineData("Cable Fly")]
    [InlineData("Barbell Curl")]
    [InlineData("Triceps Pushdown")]
    [InlineData("Leg Curl")]
    public void TheCatalogTypesTheseAsIsolation(string name)
    {
        var exercise = ExerciseCatalog.Find(name);

        Assert.NotNull(exercise);
        Assert.Equal(ExerciseType.Isolation, exercise!.Type);
    }

    [Theory]
    [InlineData("Bench Press")]
    [InlineData("Back Squat")]
    [InlineData("Deadlift")]
    [InlineData("Barbell Row")]
    public void TheCatalogTypesTheseAsCompound(string name)
    {
        var exercise = ExerciseCatalog.Find(name);

        Assert.NotNull(exercise);
        Assert.Equal(ExerciseType.Compound, exercise!.Type);
    }

    /// <summary>
    /// Složene vežbe koje opseg snage ne podnose: na jednoj nozi, nestabilne, ili bez načina
    /// da se doda opterećenje. Pre ovog pravila napredni vežbač je na šablonu Legs
    /// Specialization tri od pet složenih mesta u bloku snage dobijao baš ovde, na 3–6.
    /// </summary>
    [Theory]
    [InlineData("Bulgarian Split Squat")]
    [InlineData("Split Squat")]
    [InlineData("Walking Lunge")]
    [InlineData("Goblet Squat")]
    [InlineData("Step-Up")]
    [InlineData("Single-Leg Romanian Deadlift")]
    [InlineData("Push-up")]
    public void AStrengthBlock_KeepsUnilateralAndUnloadableCompoundsOnTheAccessoryRange(string name)
    {
        var exercise = ExerciseCatalog.Find(name)!;

        Assert.Equal(ExerciseType.Compound, exercise.Type);
        Assert.False(exercise.SuitsLowReps);

        var prescription = GoalPrescriptions.ForExercise(Goal.Strength, exercise.Type, exercise.SuitsLowReps);
        var accessory = GoalPrescriptions.ForGoal(Goal.Hypertrophy);
        Assert.Equal(accessory.RepRangeMin, prescription.RepRangeMin);
        Assert.Equal(accessory.RepRangeMax, prescription.RepRangeMax);
        Assert.Equal(GoalPrescriptions.ForGoal(Goal.Strength).TargetRir, prescription.TargetRir);
        Assert.False(GoalPrescriptions.IsStrengthLift(Goal.Strength, exercise.Type, exercise.SuitsLowReps));
    }

    [Fact]
    public void AStrengthLift_IsOnlyAStrengthLiftInAStrengthBlock()
    {
        Assert.True(GoalPrescriptions.IsStrengthLift(Goal.Strength, ExerciseType.Compound, suitsLowReps: true));
        Assert.False(GoalPrescriptions.IsStrengthLift(Goal.Hypertrophy, ExerciseType.Compound, suitsLowReps: true));
        Assert.False(GoalPrescriptions.IsStrengthLift(Goal.Strength, ExerciseType.Isolation, suitsLowReps: true));
    }

    [Fact]
    public void AHypertrophyBlock_IgnoresTheLowRepFlag()
    {
        // Za hipertrofiju je svaka vežba u opsegu cilja - zastavica tamo ništa ne menja.
        Assert.Equal(
            GoalPrescriptions.ForExercise(Goal.Hypertrophy, ExerciseType.Compound, suitsLowReps: true),
            GoalPrescriptions.ForExercise(Goal.Hypertrophy, ExerciseType.Compound, suitsLowReps: false));
    }
}
