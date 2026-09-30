using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Objašnjenje koje korisnik vidi posle treninga: koja je vežba pomerena, i zbog kog mišića.
/// Ono mora da kaže istinu u svakom slučaju koji balansiranje poznaje - nedelja daleko od
/// cilja, nedelja preko MRV-a, i trening sa previše serija jednog mišića.
/// </summary>
public class SetChangeExplanationTests
{
    private static readonly Guid Chest = new("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Back = new("00000000-0000-0000-0000-0000000000c2");
    private static readonly Guid Biceps = new("00000000-0000-0000-0000-0000000000c3");
    private static readonly Guid Triceps = new("00000000-0000-0000-0000-0000000000c4");
    private static readonly Guid Session = new("00000000-0000-0000-0001-000000000001");

    [Fact]
    public void ACutTheCeilingMade_IsExplainedByTheCeiling_EvenWithTheWeekBelowTarget()
    {
        // Push dan Push/Pull/Legs: bez granice bi razvlačenje ostalo na 4, a grudi na 12 u
        // treningu. Nedelja traži 16, pa nedeljni cilj ovaj rez ne može da objasni.
        var fly = Slot(Chest, prescribed: 4);

        var cause = SetChangeExplanation.Explain(
            fly,
            previousSets: 4,
            allocatedSets: 3,
            Week(
                withoutCeiling: (fly, 4),
                stimulative: [(Chest, 11m)],
                perSessionWithoutCeiling: [(Chest, 12m)],
                targets: [(Chest, 16m, 22m)]));

        Assert.Equal(new SetChangeCause(Chest, SetChangeReason.SessionCeiling), cause);
    }

    [Fact]
    public void ASwapInsideAFullSession_IsNotBlamedOnTheCeiling()
    {
        // Nalaz iz revizije, Upper/Lower + PPL posle prvog dana: veslanje (leđa + pola
        // bicepsa) 4 -> 3, a opružanje ruku sa sajlom (samo leđa) 3 -> 4. Leđa u treningu
        // stoje na 11 i pre i posle; rez traži biceps, pola serije iznad cilja. Vraćanje
        // samo veslanja bi trening dovelo na 12 - ali granica ga nije spustila: i bez nje
        // bi stajalo na 3.
        var row = new ExerciseSetSlot(
            Guid.NewGuid(), Session, 4, [new MuscleLoad(Back, 1.0m), new MuscleLoad(Biceps, 0.5m)]);

        var cause = SetChangeExplanation.Explain(
            row,
            previousSets: 4,
            allocatedSets: 3,
            Week(
                withoutCeiling: (row, 3),
                stimulative: [(Back, 18m), (Biceps, 14m)],
                perSessionWithoutCeiling: [(Back, 11m), (Biceps, 5m)],
                targets: [(Back, 18m, 25m), (Biceps, 14m, 20m)]));

        Assert.NotEqual(SetChangeReason.SessionCeiling, cause?.Reason);
    }

    [Fact]
    public void ACut_IsReadFromWhatTheLifterSaw_NotFromThePrescription()
    {
        // Veslanje je propisano sa 4, lifter ga je video na 6, a sada je 5. Prema propisu to
        // je i dalje "podignuto", pa je staro čitanje tražilo mišić ispod cilja - i nalazilo
        // biceps, pored strelice nadole. Nedelja leđa je iznad cilja, i to je razlog.
        var row = new ExerciseSetSlot(
            Guid.NewGuid(), Session, 4, [new MuscleLoad(Back, 1.0m), new MuscleLoad(Biceps, 0.5m)]);

        var cause = SetChangeExplanation.Explain(
            row,
            previousSets: 6,
            allocatedSets: 5,
            Week(
                withoutCeiling: (row, 5),
                stimulative: [(Back, 18m), (Biceps, 8m)],
                perSessionWithoutCeiling: [(Back, 9m), (Biceps, 4m)],
                targets: [(Back, 17m, 25m), (Biceps, 14m, 20m)]));

        Assert.Equal(new SetChangeCause(Back, SetChangeReason.WeeklyTarget), cause);
    }

    [Fact]
    public void ACutForRecovery_NamesTheMuscleEvenBelowItsTarget()
    {
        // Lake serije (RIR 5+) ne daju stimulus, ali troše oporavak: stimulativno je triceps
        // na 9 od 18, a sirovo na 17,5 od MRV-a 18. Rez tu traži MRV, i mora da ga imenuje.
        var pushdown = Slot(Triceps, prescribed: 5);

        var cause = SetChangeExplanation.Explain(
            pushdown,
            previousSets: 5,
            allocatedSets: 4,
            Week(
                withoutCeiling: (pushdown, 4),
                stimulative: [(Triceps, 9m)],
                raw: [(Triceps, 17.5m)],
                perSessionWithoutCeiling: [(Triceps, 4m)],
                targets: [(Triceps, 18m, 18m)]));

        Assert.Equal(new SetChangeCause(Triceps, SetChangeReason.WeeklyTarget), cause);
    }

    [Fact]
    public void ARaise_IsExplainedByTheMuscleTheWeekIsShortOf()
    {
        var curl = Slot(Biceps, prescribed: 3);

        var cause = SetChangeExplanation.Explain(
            curl,
            previousSets: 3,
            allocatedSets: 4,
            Week(
                withoutCeiling: (curl, 4),
                stimulative: [(Biceps, 12m)],
                perSessionWithoutCeiling: [(Biceps, 4m)],
                targets: [(Biceps, 14m, 20m)]));

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
            Week(
                withoutCeiling: (fly, 6),
                stimulative: [(Chest, 17m)],
                perSessionWithoutCeiling: [(Chest, 13m)],
                targets: [(Chest, 16m, 22m)]));

        Assert.Null(cause);
    }

    [Fact]
    public void AMoveNoLimitAskedFor_HasNoCause()
    {
        // Bez ovog poteza nedelja bi stajala tačno na cilju (17 + 1 = 18), daleko ispod
        // MRV-a, a trening ispod granice: rez nije tražio nijedan limit, pa ekran ne sme da
        // ga objasni.
        var fly = Slot(Chest, prescribed: 4);

        var cause = SetChangeExplanation.Explain(
            fly,
            previousSets: 5,
            allocatedSets: 4,
            Week(
                withoutCeiling: (fly, 4),
                stimulative: [(Chest, 17m)],
                perSessionWithoutCeiling: [(Chest, 8m)],
                targets: [(Chest, 18m, 22m)]));

        Assert.Null(cause);
    }

    [Fact]
    public void AnUnchangedProposal_HasNoCause()
    {
        var fly = Slot(Chest, prescribed: 4);

        Assert.Null(SetChangeExplanation.Explain(
            fly,
            previousSets: 4,
            allocatedSets: 4,
            Week(
                withoutCeiling: (fly, 6),
                stimulative: [(Chest, 20m)],
                perSessionWithoutCeiling: [(Chest, 20m)],
                targets: [(Chest, 16m, 22m)])));
    }

    [Fact]
    public void EveryCutTheCeilingMakesOnABuiltInWeek_IsExplainedByTheCeiling()
    {
        // Svaka nedelja svakog ugrađenog šablona, pri generisanju: vežba koju granica spusti
        // ispod onoga što bi dobila bez nje, u treningu koji bi bez granice bio preko nje,
        // mora da dobije objašnjenje "pun trening" - a ne tišinu ili mišić ispod cilja.
        //
        // Posredni rezovi se broje posebno: vežba niža nego bez granice u treningu koji ni
        // bez nje ne bi bio pun (pretraga je zbog granice drugde završila u drugom rasporedu).
        // Tu "pun trening" ne bi bilo tačno, pa takav rez tu oznaku ne sme da dobije.
        var unexplained = new List<string>();
        var ceilingCuts = 0;
        var knockOnLabelled = new List<string>();
        var knockOnCuts = 0;

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            var without = week.AllocatedWithoutSessionCeiling();
            var balanced = Balanced(week.Slots, week.Targets, week.Allocated, without, NoVolume);

            foreach (var slot in week.Slots.Where(slot => week.Allocated[slot.Id] < without[slot.Id]))
            {
                var cause = SetChangeExplanation.Explain(slot, without[slot.Id], week.Allocated[slot.Id], balanced);
                var sessionWouldBeFull = slot.Muscles.Any(muscle =>
                    balanced.Targets.ContainsKey(muscle.MuscleGroupId)
                    && balanced.PerSessionWithoutSessionCeiling.GetValueOrDefault((slot.SessionId, muscle.MuscleGroupId))
                       > TrainingConstants.MaxSetsPerMusclePerSession);

                if (sessionWouldBeFull)
                {
                    ceilingCuts++;
                    if (cause?.Reason != SetChangeReason.SessionCeiling)
                    {
                        unexplained.Add($"{week.Name}: {without[slot.Id]} -> {week.Allocated[slot.Id]}, "
                                        + $"objašnjeno kao {cause?.Reason.ToString() ?? "ništa"}");
                    }
                }
                else
                {
                    knockOnCuts++;
                    if (cause?.Reason == SetChangeReason.SessionCeiling)
                    {
                        knockOnLabelled.Add($"{week.Name}: {without[slot.Id]} -> {week.Allocated[slot.Id]}");
                    }
                }
            }
        }

        Assert.True(ceilingCuts > 0, "Nijedan rez granice - test ne proverava ništa.");
        Assert.True(knockOnCuts > 0, "Nijedan posredan rez - drugi deo testa ne proverava ništa.");
        Assert.True(unexplained.Count == 0, string.Join(Environment.NewLine, unexplained.Take(20)));
        Assert.True(knockOnLabelled.Count == 0, string.Join(Environment.NewLine, knockOnLabelled.Take(20)));
    }

    [Fact]
    public void MidWeek_ACeilingLabelAlwaysMeansTheCeilingLoweredTheExercise()
    {
        // Put iz revizije: prvi dan odrađen kako je predložen, pa balansiranje ostatka
        // nedelje. Oznaka "pun trening" sme da stoji samo tamo gde bi bez granice vežba
        // ostala viša - u zamenama unutar punog treninga ranije je stajala i bez toga.
        var falseLabels = new List<string>();
        var labels = 0;

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            var firstDay = week.Slots[0].SessionId;
            var done = week.Slots.Where(slot => slot.SessionId == firstDay).ToList();
            var open = week.Slots.Where(slot => slot.SessionId != firstDay).ToList();
            if (open.Count == 0)
            {
                continue;
            }

            // Serije na ciljnom RIR-u: stimulativno i sirovo su isto.
            var banked = WeeklySetAllocation.Project(done, week.Allocated, NoVolume);
            var allocated = WeeklySetAllocation.Allocate(open, week.Targets, banked, banked);
            var without = WeeklySetAllocation.AllocateWithoutSessionCeiling(open, week.Targets, banked, banked);
            var balanced = Balanced(open, week.Targets, allocated, without, banked);

            foreach (var slot in open.Where(slot => allocated[slot.Id] != week.Allocated[slot.Id]))
            {
                var cause = SetChangeExplanation.Explain(slot, week.Allocated[slot.Id], allocated[slot.Id], balanced);
                if (cause?.Reason != SetChangeReason.SessionCeiling)
                {
                    continue;
                }

                labels++;
                if (allocated[slot.Id] >= without[slot.Id])
                {
                    falseLabels.Add($"{week.Name}: {week.Allocated[slot.Id]} -> {allocated[slot.Id]}, "
                                    + $"bez granice {without[slot.Id]}");
                }
            }
        }

        Assert.True(labels > 0, "Nijedna oznaka granice - test ne proverava ništa.");
        Assert.True(falseLabels.Count == 0, string.Join(Environment.NewLine, falseLabels.Take(20)));
    }

    // --- helpers --------------------------------------------------------------

    private static readonly IReadOnlyDictionary<Guid, decimal> NoVolume = new Dictionary<Guid, decimal>();

    private static ExerciseSetSlot Slot(Guid muscle, int prescribed)
    {
        return new ExerciseSetSlot(Guid.NewGuid(), Session, prescribed, [new MuscleLoad(muscle, 1.0m)]);
    }

    private static BalancedWeek Week(
        (ExerciseSetSlot Slot, int Sets) withoutCeiling,
        (Guid Muscle, decimal Sets)[] stimulative,
        (Guid Muscle, decimal Sets)[] perSessionWithoutCeiling,
        (Guid Muscle, decimal Target, decimal Mrv)[] targets,
        (Guid Muscle, decimal Sets)[]? raw = null)
    {
        return new BalancedWeek(
            stimulative.ToDictionary(entry => entry.Muscle, entry => entry.Sets),
            (raw ?? stimulative).ToDictionary(entry => entry.Muscle, entry => entry.Sets),
            new Dictionary<Guid, int> { [withoutCeiling.Slot.Id] = withoutCeiling.Sets },
            perSessionWithoutCeiling.ToDictionary(entry => (Session, entry.Muscle), entry => entry.Sets),
            targets.ToDictionary(
                entry => entry.Muscle,
                entry => new MuscleVolumeTarget(entry.Muscle, entry.Target, entry.Mrv)));
    }

    private static BalancedWeek Balanced(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyList<MuscleVolumeTarget> targets,
        IReadOnlyDictionary<Guid, int> allocated,
        IReadOnlyDictionary<Guid, int> withoutCeiling,
        IReadOnlyDictionary<Guid, decimal> banked)
    {
        return new BalancedWeek(
            WeeklySetAllocation.Project(slots, allocated, banked),
            WeeklySetAllocation.Project(slots, allocated, banked),
            withoutCeiling,
            WeeklySetAllocation.ProjectPerSession(slots, withoutCeiling),
            targets.ToDictionary(target => target.MuscleGroupId));
    }
}
