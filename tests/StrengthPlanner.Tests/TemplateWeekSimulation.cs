using System.Security.Cryptography;
using System.Text;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Jedna nedelja ugrađenog šablona, sastavljena istim domenskim pozivima kojima je sastavljaju
/// generator i balansiranje serija — bez baze.
///
/// Postoji zato što katalog-testovi dugo nisu videli balansiranje: sabirali su propis, a
/// korisnik dobija ono što alokator od propisa napravi. Ono što treba izmeriti na predlogu
/// (koliko serija jedan mišić dobije u jednom treningu) na propisu ne postoji.
/// </summary>
internal static class TemplateWeekSimulation
{
    public static Guid MuscleId(string muscle)
    {
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(muscle)));
    }

    /// <summary>Svaka trenažna nedelja svakog ugrađenog šablona, na svakom nivou, cilju i modelu.</summary>
    public static IEnumerable<SimulatedWeek> EveryTrainingWeek()
    {
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        foreach (var goal in Enum.GetValues<Goal>())
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            for (var weekNumber = 1; weekNumber <= Periodization.DurationWeeks(model); weekNumber++)
            {
                if (!IsDeload(model, weekNumber))
                {
                    yield return Build(template, level, goal, model, weekNumber);
                }
            }
        }
    }

    public static SimulatedWeek Build(
        WorkoutTemplate template,
        ExperienceLevel level,
        Goal goal,
        PeriodizationModel model,
        int weekNumber)
    {
        var slots = Slots(template, level, goal, model, weekNumber);
        var prescribed = PrescribedVolume(slots);

        // Isto kao WeeklyVolumeTargetResolver: odnos prema osnovnoj nedelji bloka.
        var baseVolume = PrescribedVolume(Slots(template, level, goal, model, Periodization.BaseWeekNumber(model)));

        var targets = ExerciseCatalog.VolumeLandmarks
            .Where(seed => prescribed.ContainsKey(MuscleId(seed.Muscle)))
            .Select(seed =>
            {
                var band = ExperienceProgramming.ScaleLandmarks(
                    new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                    level);
                var muscleGroupId = MuscleId(seed.Muscle);

                return new MuscleVolumeTarget(
                    muscleGroupId,
                    WeeklyVolumeTarget.ForWeek(
                        goal,
                        band,
                        prescribed[muscleGroupId],
                        baseVolume.GetValueOrDefault(muscleGroupId)),
                    band.Mrv);
            })
            .ToList();

        return new SimulatedWeek(
            $"{template.Key} {level} {goal} {model} w{weekNumber}",
            template,
            level,
            slots,
            targets,
            WeeklySetAllocation.Allocate(slots, targets));
    }

    private static List<ExerciseSetSlot> Slots(
        WorkoutTemplate template,
        ExperienceLevel level,
        Goal goal,
        PeriodizationModel model,
        int weekNumber)
    {
        var goalSettings = GoalPrescriptions.ForGoal(goal);
        var startingSets = ExperienceProgramming.StartingSetsPerExercise(level);
        var slots = new List<ExerciseSetSlot>();

        for (var dayIndex = 0; dayIndex < template.Days.Count; dayIndex++)
        {
            var exerciseNames = SessionComposition.ForLevel(
                template.Days[dayIndex].Exercises,
                ExerciseCatalog.IsCompound,
                level);

            for (var exerciseIndex = 0; exerciseIndex < exerciseNames.Count; exerciseIndex++)
            {
                var exercise = ExerciseCatalog.Find(exerciseNames[exerciseIndex])!;
                var settings = GoalPrescriptions.ForExercise(goal, exercise.Type);
                var week = Periodization.ForWeek(
                    model,
                    weekNumber,
                    settings.RepRangeMin,
                    settings.RepRangeMax,
                    goalSettings.TargetRir,
                    startingSets);

                slots.Add(new ExerciseSetSlot(
                    new Guid(dayIndex + 1, (short)(exerciseIndex + 1), 0, new byte[8]),
                    SessionIdOf(dayIndex),
                    week.Sets,
                    exercise.Muscles
                        .Select(muscle => new MuscleLoad(MuscleId(muscle.Muscle), muscle.Contribution))
                        .ToList()));
            }
        }

        return slots;
    }

    public static Guid SessionIdOf(int dayIndex)
    {
        return new Guid(dayIndex + 1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
    }

    private static Dictionary<Guid, decimal> PrescribedVolume(IReadOnlyList<ExerciseSetSlot> slots)
    {
        return WeeklySetAllocation.Project(
            slots,
            slots.ToDictionary(slot => slot.Id, slot => slot.PrescribedSets),
            new Dictionary<Guid, decimal>());
    }

    private static bool IsDeload(PeriodizationModel model, int weekNumber)
    {
        return Periodization.ForWeek(model, weekNumber, 8, 12, 1, 3).IsDeload;
    }
}

/// <summary>Jedna izbalansirana nedelja ugrađenog šablona.</summary>
internal sealed record SimulatedWeek(
    string Name,
    WorkoutTemplate Template,
    ExperienceLevel Level,
    IReadOnlyList<ExerciseSetSlot> Slots,
    IReadOnlyList<MuscleVolumeTarget> Targets,
    IReadOnlyDictionary<Guid, int> Allocated)
{
    public decimal Weekly(string muscle)
    {
        return WeeklySetAllocation
            .Project(Slots, Allocated, new Dictionary<Guid, decimal>())
            .GetValueOrDefault(TemplateWeekSimulation.MuscleId(muscle));
    }

    public decimal InSession(int dayIndex, string muscle)
    {
        return WeeklySetAllocation
            .ProjectPerSession(Slots, Allocated)
            .GetValueOrDefault((TemplateWeekSimulation.SessionIdOf(dayIndex), TemplateWeekSimulation.MuscleId(muscle)));
    }

    /// <summary>
    /// Isti propis i isti ciljevi, ali sa svakom vežbom u zasebnom treningu — dakle bez
    /// granice po treningu, jer jedna vežba ne može da nosi više od šest serija. Tako je
    /// alokator radio pre granice, pa je ovo poređenje "pre" i "posle" na istom ulazu.
    /// </summary>
    public IReadOnlyDictionary<Guid, int> AllocatedWithoutSessionCeiling()
    {
        var oneSessionEach = Slots
            .Select(slot => slot with { SessionId = slot.Id })
            .ToList();

        return WeeklySetAllocation.Allocate(oneSessionEach, Targets);
    }

    public MuscleVolumeTarget? TargetFor(string muscle)
    {
        var muscleGroupId = TemplateWeekSimulation.MuscleId(muscle);
        return Targets.FirstOrDefault(target => target.MuscleGroupId == muscleGroupId);
    }
}
