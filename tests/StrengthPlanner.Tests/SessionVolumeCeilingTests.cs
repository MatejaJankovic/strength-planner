using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Granica serija po mišiću u jednom treningu, merena na predlogu koji korisnik zaista dobija:
/// svaki ugrađen šablon, na svakom nivou, cilju i modelu periodizacije, posle balansiranja.
///
/// Pre granice je Push dan šablona Push/Pull/Legs nosio 16 do 18 serija za grudi, a u 473
/// kombinacije trening-mišić predlog je stajao preko jedanaest.
/// </summary>
public class SessionVolumeCeilingTests
{
    /// <summary>
    /// Trening sme da ostane preko granice samo ako je balansiranje potrošilo ceo prozor:
    /// svaka vežba kojoj je taj mišić glavni već stoji na najnižem što propis dozvoljava.
    /// Merilo nije "nikad preko jedanaest", jer propis od osamnaest serija sa dozvoljenim
    /// pomakom od dve po vežbi ne može da stigne ispod dvanaest.
    /// </summary>
    [Fact]
    public void EveryBuiltInWeek_StaysUnderTheSessionCeiling_WhereverThePrescriptionAllows()
    {
        var breaches = new List<string>();

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            var perSession = WeeklySetAllocation.ProjectPerSession(week.Slots, week.Allocated);

            foreach (var ((sessionId, muscleGroupId), sets) in perSession)
            {
                if (sets <= TrainingConstants.MaxSetsPerMusclePerSession
                    || week.TargetFor(MuscleName(muscleGroupId)) is null)
                {
                    continue;
                }

                var stillMovable = week.Slots
                    .Where(slot => slot.SessionId == sessionId
                                   && slot.Muscles.Any(muscle => muscle.MuscleGroupId == muscleGroupId
                                                                 && muscle.Contribution >= 1m))
                    .Where(slot => week.Allocated[slot.Id] > LowestAllowed(slot))
                    .ToList();

                if (stillMovable.Count > 0)
                {
                    breaches.Add($"{week.Name} {MuscleName(muscleGroupId)}: {sets} serija u treningu, "
                                 + $"a {stillMovable.Count} vežbi još može niže.");
                }
            }
        }

        Assert.True(breaches.Count == 0, string.Join(Environment.NewLine, breaches.Take(20)));
    }

    /// <summary>
    /// Gornja granica pojedinačnog treninga, u brojevima: najviše jedna serija preko, i to
    /// samo tamo gde je propis sam bio daleko iznad. Važi i posle toga što glavna dizanja
    /// bloka snage nedeljni cilj više ne pomera - granicu treninga i dalje mogu da spuste.
    /// </summary>
    [Fact]
    public void NoBuiltInSession_GoesMoreThanOneSetPastTheCeiling()
    {
        var worst = TemplateWeekSimulation.EveryTrainingWeek()
            .SelectMany(week => WeeklySetAllocation.ProjectPerSession(week.Slots, week.Allocated).Values)
            .Max();

        Assert.True(
            worst <= TrainingConstants.MaxSetsPerMusclePerSession + 1,
            $"Najveći trening nosi {worst} serija za jedan mišić.");
    }

    /// <summary>
    /// Push/Pull/Legs trenira svaki mišić jednom nedeljno, pa ceo nedeljni volumen grudi pada
    /// u jedan trening. Uz granicu nedelja ostaje ispod MAV-a — to nije greška šablona nego
    /// njegova frekvencija, i zato šablon nosi upozorenje, kao i dvodnevni.
    /// </summary>
    [Fact]
    public void PushPullLegs_StaysBelowMav_BecauseEachMuscleIsTrainedOnce()
    {
        var template = WorkoutTemplateCatalog.GetByKey(WorkoutTemplateCatalog.PushPullLegsKey)!;
        var week = TemplateWeekSimulation.Build(
            template,
            ExperienceLevel.Intermediate,
            Goal.Hypertrophy,
            PeriodizationModel.Flat,
            weekNumber: 1);

        Assert.False(string.IsNullOrWhiteSpace(template.Note));

        var chest = week.Weekly("Chest");
        Assert.Equal(TrainingConstants.MaxSetsPerMusclePerSession, chest);
        Assert.True(chest < week.TargetFor("Chest")!.TargetSets);
    }

    /// <summary>
    /// Full Body, Upper/Lower i Upper/Lower x3 na referentnom nivou granicu ne osećaju: njihov
    /// najveći trening nosi 11, 8 i 7 serija jednog mišića, pa je predlog isti kao pre granice,
    /// vežba po vežba. To ne važi za svaki šablon koji mišić trenira dva puta nedeljno - Full
    /// Body (4 dana), Upper/Lower + PPL i Legs Specialization i tu imaju jedan dan preko nje.
    /// </summary>
    [Theory]
    [InlineData(WorkoutTemplateCatalog.FullBodyKey)]
    [InlineData(WorkoutTemplateCatalog.UpperLowerKey)]
    [InlineData(WorkoutTemplateCatalog.UpperLowerThreeXKey)]
    public void TemplatesThatSplitTheWeek_AreNotTouchedAtTheReferenceLevel(string templateKey)
    {
        var week = TemplateWeekSimulation.Build(
            WorkoutTemplateCatalog.GetByKey(templateKey)!,
            ExperienceLevel.Intermediate,
            Goal.Hypertrophy,
            PeriodizationModel.Flat,
            weekNumber: 1);

        Assert.Equal(week.AllocatedWithoutSessionCeiling(), week.Allocated);
    }

    /// <summary>
    /// Granica menja samo nedelje u kojima bi neki trening bez nje prešao granicu. Svaka
    /// druga nedelja svakog šablona dobija isti predlog kao pre.
    ///
    /// Dva izuzetka, imenom: u nedeljama 4 i 5 linearnog modela Full Body (4 dana) za
    /// početnika u bloku snage propis već stavlja 12 serija jednog mišića u trening. Predlog
    /// bez granice završi na 11, ali drugim putem - granica ceni već prvi korak pretrage.
    /// Uzrok postoji, samo ga predlog bez granice ne pokazuje. Spisak mora da se poklopi
    /// tačno: nedelja koja prestane da bude izuzetak mora i da se skine sa njega.
    /// </summary>
    [Fact]
    public void TheCeiling_ChangesOnlyWeeksThatWouldBreachIt()
    {
        var breachedOnlyInThePrescription = new[]
        {
            "full-body-4 Beginner Strength LinearRising w4",
            "full-body-4 Beginner Strength LinearRising w5",
        };
        var changedWithoutCause = new List<string>();

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            var before = week.AllocatedWithoutSessionCeiling();
            var wouldBreach = WeeklySetAllocation
                .ProjectPerSession(week.Slots, before)
                .Any(entry => entry.Value > TrainingConstants.MaxSetsPerMusclePerSession
                              && week.TargetFor(MuscleName(entry.Key.MuscleGroupId)) is not null);

            if (!wouldBreach && !before.SequenceEqual(week.Allocated))
            {
                changedWithoutCause.Add(week.Name);
            }
        }

        Assert.True(
            changedWithoutCause.SequenceEqual(breachedOnlyInThePrescription),
            string.Join(Environment.NewLine, changedWithoutCause.Take(20)));

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek().Where(week => breachedOnlyInThePrescription.Contains(week.Name)))
        {
            var prescribed = week.Slots.ToDictionary(slot => slot.Id, slot => slot.PrescribedSets);
            Assert.Contains(
                WeeklySetAllocation.ProjectPerSession(week.Slots, prescribed),
                entry => entry.Value > TrainingConstants.MaxSetsPerMusclePerSession
                         && week.TargetFor(MuscleName(entry.Key.MuscleGroupId)) is not null);
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

    private static string MuscleName(Guid muscleGroupId)
    {
        return ExerciseCatalog.MuscleGroupNames.First(name => TemplateWeekSimulation.MuscleId(name) == muscleGroupId);
    }
}
