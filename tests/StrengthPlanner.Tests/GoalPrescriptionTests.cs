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
    /// <summary>
    /// Izolacija nosi svoj opseg, 10-20, u oba bloka. Do runde 14 je nosila 8-12, opseg
    /// hipertrofije složenih vežbi - i u bloku snage, gde je to već bio pomoćni rad.
    /// </summary>
    [Theory]
    [InlineData(Goal.Strength)]
    [InlineData(Goal.Hypertrophy)]
    public void AnIsolation_CarriesItsOwnRange_InEitherBlock(Goal goal)
    {
        var isolation = GoalPrescriptions.ForExercise(goal, ExerciseType.Isolation, suitsLowReps: true);

        Assert.Equal(10, isolation.RepRangeMin);
        Assert.Equal(20, isolation.RepRangeMax);
        Assert.False(GoalPrescriptions.CarriesTheGoalRange(goal, ExerciseType.Isolation, suitsLowReps: true));
    }

    [Fact]
    public void AStrengthBlock_KeepsItsCompoundsOnTheStrengthRange()
    {
        var compound = GoalPrescriptions.ForExercise(Goal.Strength, ExerciseType.Compound, suitsLowReps: true);

        Assert.Equal(3, compound.RepRangeMin);
        Assert.Equal(6, compound.RepRangeMax);
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
    public void AHypertrophyBlock_KeepsItsCompoundsOnTheGoalRange()
    {
        Assert.Equal(
            GoalPrescriptions.ForGoal(Goal.Hypertrophy),
            GoalPrescriptions.ForExercise(Goal.Hypertrophy, ExerciseType.Compound, suitsLowReps: true));
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
                PeriodizationModel.LinearRising,
                week,
                prescription.RepRangeMin,
                prescription.RepRangeMax,
                prescription.TargetRir,
                4);

            return $"{result.RepRangeMin}-{result.RepRangeMax}";
        }

        // Najteža nedelja: složena vežba ide na trojku, izolacija ostaje u opsegu u kome
        // izolacija i ima smisla.
        Assert.Equal("3-4", Window(compound, 5));
        Assert.Equal("8-18", Window(isolation, 5));

        // Deload vraća osnovni opseg svake vežbe, pa i tu ostaju razdvojeni.
        Assert.Equal("3-6", Window(compound, 6));
        Assert.Equal("10-20", Window(isolation, 6));
    }

    /// <summary>
    /// Opseg odlučuje i o polaznom opterećenju, pa izolacija sa poznatim maksimumom u bloku
    /// snage startuje lakše — što je i smisao izmene. Za bočno podizanje sa maksimumom od
    /// 40 kg: propis 10–20 pri RIR 2 traži dvanaest efektivnih ponavljanja, a 3–6 samo pet.
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
        Assert.Equal(27.5m, onIsolationRange);
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

    [Theory]
    [InlineData(ExerciseType.Compound, 6, true)]
    [InlineData(ExerciseType.Compound, 5, true)]
    [InlineData(ExerciseType.Compound, 12, false)]
    [InlineData(ExerciseType.Compound, 15, false)]
    [InlineData(ExerciseType.Isolation, 6, false)]
    public void AMainLift_IsReadFromThePlansOwnRange(ExerciseType type, int baseRepRangeMax, bool expected)
    {
        // Lični šablon sam bira opseg: leg press na 12-15 je u njegovom bloku snage pomoćni
        // rad, a iskorak na 3-5 je dizanje koje je izabrao da optereti.
        Assert.Equal(expected, GoalPrescriptions.IsMainLift(Goal.Strength, type, baseRepRangeMax));
        Assert.False(GoalPrescriptions.IsMainLift(Goal.Hypertrophy, type, baseRepRangeMax));
    }

    [Fact]
    public void ForABuiltInTemplate_TheTwoReadingsOfAMainLiftAgree()
    {
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            var prescription = GoalPrescriptions.ForExercise(Goal.Strength, exercise.Type, exercise.SuitsLowReps);

            Assert.Equal(
                GoalPrescriptions.IsStrengthLift(Goal.Strength, exercise.Type, exercise.SuitsLowReps),
                GoalPrescriptions.IsMainLift(Goal.Strength, exercise.Type, prescription.RepRangeMax));
        }
    }

    /// <summary>
    /// Lični šablon sme izolaciji da propiše do 20 ponavljanja, a složenoj vežbi do 12:
    /// preko toga složena vežba ne daje procenu maksimuma, a nju čitaju trend snage, rekordi
    /// i ocena umora.
    /// </summary>
    [Fact]
    public void ACustomTemplate_MayGoToTwenty_OnlyForAnIsolation()
    {
        Assert.Equal(20, GoalPrescriptions.MaxTemplateReps(ExerciseType.Isolation));
        Assert.Equal(12, GoalPrescriptions.MaxTemplateReps(ExerciseType.Compound));
        Assert.Equal(TrainingConstants.EpleyRepCap, GoalPrescriptions.MaxTemplateReps(ExerciseType.Compound));
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
