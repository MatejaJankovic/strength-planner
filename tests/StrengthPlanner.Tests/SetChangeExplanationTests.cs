using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Objašnjenje koje korisnik vidi posle treninga: koja je vežba pomerena, i zbog kog mišića.
/// Ono mora da kaže istinu u oba slučaja koje balansiranje poznaje - nedelja daleko od cilja,
/// i trening sa previše serija jednog mišića.
/// </summary>
public class SetChangeExplanationTests
{
    private static readonly Guid Chest = new("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Back = new("00000000-0000-0000-0000-0000000000c2");
    private static readonly Guid Biceps = new("00000000-0000-0000-0000-0000000000c3");
    private static readonly Guid Session = new("00000000-0000-0000-0001-000000000001");

    [Fact]
    public void ACutThatBringsASessionUnderTheCeiling_IsExplainedByTheCeiling_EvenWithTheWeekBelowTarget()
    {
        // Push dan Push/Pull/Legs: grudi 12 u treningu, a nedelja traži 16. Nedeljni cilj
        // ovaj rez ne može da objasni - on bi grudi hteo više, ne manje.
        var fly = Slot(Chest, prescribed: 4);

        var cause = SetChangeExplanation.Explain(
            fly,
            previousSets: 4,
            allocatedSets: 3,
            Weekly((Chest, 11m)),
            PerSession((Chest, 11m)),
            Targets((Chest, 16m)));

        Assert.Equal(new SetChangeCause(Chest, SetChangeReason.SessionCeiling), cause);
    }

    [Fact]
    public void ACut_IsReadFromWhatTheLifterSaw_NotFromThePrescription()
    {
        // Veslanje je propisano sa 4, lifter ga je video na 6, a sada je 5. Prema propisu to
        // je i dalje "podignuto", pa je staro čitanje tražilo mišić ispod cilja - i nalazilo
        // biceps, pored strelice nadole. Nedelja leđa je iznad cilja, i to je razlog.
        var row = new ExerciseSetSlot(
            Guid.NewGuid(),
            Session,
            4,
            [new MuscleLoad(Back, 1.0m), new MuscleLoad(Biceps, 0.5m)]);

        var cause = SetChangeExplanation.Explain(
            row,
            previousSets: 6,
            allocatedSets: 5,
            Weekly((Back, 18m), (Biceps, 8m)),
            PerSession((Back, 9m), (Biceps, 4m)),
            Targets((Back, 17m), (Biceps, 14m)));

        Assert.Equal(new SetChangeCause(Back, SetChangeReason.WeeklyTarget), cause);
    }

    [Fact]
    public void ARaise_IsExplainedByTheMuscleTheWeekIsShortOf()
    {
        var curl = Slot(Biceps, prescribed: 3);

        var cause = SetChangeExplanation.Explain(
            curl,
            previousSets: 3,
            allocatedSets: 4,
            Weekly((Biceps, 12m)),
            PerSession((Biceps, 4m)),
            Targets((Biceps, 14m)));

        Assert.Equal(new SetChangeCause(Biceps, SetChangeReason.WeeklyTarget), cause);
    }

    [Fact]
    public void ARaise_IsNeverBlamedOnTheCeiling()
    {
        // Pun trening nikad ne traži više serija. Nedelja je i bez ovog poteza na cilju, pa
        // podizanje ne objašnjava ni ona - a granica ga ne sme objasniti ni tada.
        var fly = Slot(Chest, prescribed: 4);

        var cause = SetChangeExplanation.Explain(
            fly,
            previousSets: 4,
            allocatedSets: 5,
            Weekly((Chest, 17m)),
            PerSession((Chest, 13m)),
            Targets((Chest, 16m)));

        Assert.Null(cause);
    }

    [Fact]
    public void AMoveNoLimitAskedFor_HasNoCause()
    {
        // Nedelja je na cilju i posle poteza, trening ispod granice: nijedan mišić ne
        // objašnjava izmenu, i ekran onda ne sme da ga izmisli.
        var fly = Slot(Chest, prescribed: 4);

        var cause = SetChangeExplanation.Explain(
            fly,
            previousSets: 5,
            allocatedSets: 4,
            Weekly((Chest, 17m)),
            PerSession((Chest, 8m)),
            Targets((Chest, 18m)));

        Assert.Null(cause);
    }

    [Fact]
    public void AnUnchangedProposal_HasNoCause()
    {
        Assert.Null(SetChangeExplanation.Explain(
            Slot(Chest, prescribed: 4),
            previousSets: 4,
            allocatedSets: 4,
            Weekly((Chest, 20m)),
            PerSession((Chest, 20m)),
            Targets((Chest, 16m))));
    }

    [Fact]
    public void EveryCutTheCeilingMakesOnABuiltInWeek_IsExplainedByTheCeiling()
    {
        // Svaka nedelja svakog ugrađenog šablona: vežba koju granica spusti ispod onoga što
        // bi dobila bez nje, u treningu koji bi bez nje bio preko granice, mora da dobije
        // objašnjenje "trening je pun" - a ne tišinu ili mišić ispod cilja.
        var unexplained = new List<string>();

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            var before = week.AllocatedWithoutSessionCeiling();
            var beforePerSession = WeeklySetAllocation.ProjectPerSession(week.Slots, before);
            var finalWeekly = WeeklySetAllocation.Project(week.Slots, week.Allocated, new Dictionary<Guid, decimal>());
            var finalPerSession = WeeklySetAllocation.ProjectPerSession(week.Slots, week.Allocated);
            var targets = week.Targets.ToDictionary(target => target.MuscleGroupId);

            foreach (var slot in week.Slots)
            {
                var sessionWasOver = slot.Muscles.Any(muscle =>
                    targets.ContainsKey(muscle.MuscleGroupId)
                    && beforePerSession.GetValueOrDefault((slot.SessionId, muscle.MuscleGroupId))
                       > TrainingConstants.MaxSetsPerMusclePerSession);

                if (!sessionWasOver || week.Allocated[slot.Id] >= before[slot.Id])
                {
                    continue;
                }

                var cause = SetChangeExplanation.Explain(
                    slot,
                    before[slot.Id],
                    week.Allocated[slot.Id],
                    finalWeekly,
                    finalPerSession,
                    targets);

                if (cause?.Reason != SetChangeReason.SessionCeiling)
                {
                    unexplained.Add($"{week.Name}: {before[slot.Id]} -> {week.Allocated[slot.Id]}, "
                                    + $"objašnjeno kao {cause?.Reason.ToString() ?? "ništa"}");
                }
            }
        }

        Assert.True(unexplained.Count == 0, string.Join(Environment.NewLine, unexplained.Take(20)));
    }

    private static ExerciseSetSlot Slot(Guid muscle, int prescribed)
    {
        return new ExerciseSetSlot(Guid.NewGuid(), Session, prescribed, [new MuscleLoad(muscle, 1.0m)]);
    }

    private static Dictionary<Guid, decimal> Weekly(params (Guid Muscle, decimal Sets)[] volume)
    {
        return volume.ToDictionary(entry => entry.Muscle, entry => entry.Sets);
    }

    private static Dictionary<(Guid SessionId, Guid MuscleGroupId), decimal> PerSession(
        params (Guid Muscle, decimal Sets)[] volume)
    {
        return volume.ToDictionary(entry => (Session, entry.Muscle), entry => entry.Sets);
    }

    private static Dictionary<Guid, MuscleVolumeTarget> Targets(params (Guid Muscle, decimal Target)[] targets)
    {
        return targets.ToDictionary(
            entry => entry.Muscle,
            entry => new MuscleVolumeTarget(entry.Muscle, entry.Target, 40m));
    }
}
