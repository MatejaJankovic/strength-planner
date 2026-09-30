using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Blok snage na predlogu koji vežbač zaista dobija: svaki ugrađen šablon, nivo i model,
/// posle balansiranja.
/// </summary>
public class StrengthBlockCompositionTests
{
    /// <summary>
    /// Nalaz iz revizije granice po treningu: balansiranje je u bloku snage sekao bench na
    /// 2 serije dok su razvlačenja zadržavala 5 - jedna serija bench-a rasterećuje i grudi i
    /// triceps. Preko ugrađenih nedelja takvih slučajeva je bilo 373. Glavno dizanje sada
    /// ide niže samo kada pomoćni rad za isti mišić u istom treningu više nema šta da da.
    /// </summary>
    [Fact]
    public void NoMainLiftIsCut_WhileAnAccessoryForItsMuscleInTheSameSessionCanStillGive()
    {
        var inversions = new List<string>();
        var strengthWeeks = 0;

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek().Where(week => week.Goal == Goal.Strength))
        {
            strengthWeeks++;

            foreach (var mainLift in week.Slots.Where(slot => slot.IsMainLift && week.Allocated[slot.Id] < slot.PrescribedSets))
            {
                var primary = mainLift.Muscles.First(muscle => muscle.Contribution >= 1m).MuscleGroupId;
                var accessory = week.Slots.FirstOrDefault(slot =>
                    !slot.IsMainLift
                    && slot.SessionId == mainLift.SessionId
                    && slot.Muscles.Any(muscle => muscle.MuscleGroupId == primary && muscle.Contribution >= 1m)
                    && week.Allocated[slot.Id] > LowestAllowed(slot));

                if (accessory is not null)
                {
                    inversions.Add($"{week.Name}: glavno dizanje {mainLift.PrescribedSets} -> {week.Allocated[mainLift.Id]}, "
                                   + $"pomoćni rad {accessory.PrescribedSets} -> {week.Allocated[accessory.Id]}");
                }
            }
        }

        Assert.True(strengthWeeks > 0);
        Assert.True(inversions.Count == 0, string.Join(Environment.NewLine, inversions.Take(20)));
    }

    /// <summary>
    /// Nedeljni cilj glavno dizanje ne pomera ni naviše: tamo gde nedelji nešto fali, to
    /// dobija pomoćni rad.
    /// </summary>
    [Fact]
    public void NoMainLiftIsEverRaisedAboveItsPrescription()
    {
        var raised = TemplateWeekSimulation.EveryTrainingWeek()
            .SelectMany(week => week.Slots
                .Where(slot => slot.IsMainLift && week.Allocated[slot.Id] > slot.PrescribedSets)
                .Select(slot => $"{week.Name}: {slot.PrescribedSets} -> {week.Allocated[slot.Id]}"))
            .ToList();

        Assert.True(raised.Count == 0, string.Join(Environment.NewLine, raised.Take(20)));
    }

    /// <summary>
    /// U bloku hipertrofije glavnih dizanja nema: sve se balansira kao i pre.
    /// </summary>
    [Fact]
    public void AHypertrophyBlock_HasNoMainLifts()
    {
        Assert.DoesNotContain(
            TemplateWeekSimulation.EveryTrainingWeek().Where(week => week.Goal == Goal.Hypertrophy),
            week => week.Slots.Any(slot => slot.IsMainLift));
    }

    /// <summary>
    /// Nijedna vežba na jednoj nozi ili bez načina da se doda teret ne stoji u bloku snage
    /// na opsegu snage. Pre ovog pravila napredni vežbač je na Legs Specialization tri od pet
    /// složenih mesta dobijao baš takve vežbe, na 3-6.
    /// </summary>
    [Fact]
    public void NoUnilateralOrUnloadableLiftIsAMainLift()
    {
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        foreach (var day in template.Days)
        foreach (var name in SessionComposition.ForLevel(day.Exercises, ExerciseCatalog.IsCompound, level, Goal.Strength))
        {
            var exercise = ExerciseCatalog.Find(name)!;
            if (!exercise.SuitsLowReps)
            {
                Assert.False(GoalPrescriptions.IsStrengthLift(Goal.Strength, exercise.Type, exercise.SuitsLowReps), name);
            }
        }
    }

    /// <summary>
    /// U svakom treningu bloka snage glavna dizanja stoje ispred ostalog - i ispred složene
    /// vežbe koja je tu pomoćni rad.
    /// </summary>
    [Fact]
    public void EveryStrengthSession_OpensWithItsMainLifts()
    {
        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek().Where(week => week.Goal == Goal.Strength))
        foreach (var session in week.Slots.GroupBy(slot => slot.SessionId))
        {
            var flags = session.Select(slot => slot.IsMainLift).ToList();
            var lastMain = flags.LastIndexOf(true);
            var firstOther = flags.IndexOf(false);

            Assert.True(
                lastMain < 0 || firstOther < 0 || lastMain < firstOther,
                $"{week.Name}: glavno dizanje posle pomoćnog rada.");
        }
    }

    private static int LowestAllowed(ExerciseSetSlot slot)
    {
        return Math.Min(
            slot.PrescribedSets,
            Math.Max(
                WeeklySetAllocation.MinSetsPerExercise,
                slot.PrescribedSets - WeeklySetAllocation.MaxDriftFromPrescription));
    }
}
