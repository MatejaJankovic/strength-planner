using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Priručnik definiše MAV kao <i>"optimalan volumen treninga pri kojem se telo adekvatno
/// oporavlja i stimuliše hipertrofiju, bez ulaska u prekomeran zamor"</i>. Ovi testovi
/// proveravaju da predlog serija zaista cilja tu vrednost — i da to ne radi po cenu
/// propisa, gornje granice oporavka ili ravnomerne raspodele kroz nedelju.
/// </summary>
public class WeeklySetAllocationTests
{
    private static readonly Guid Chest = new("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Triceps = new("00000000-0000-0000-0000-0000000000c2");
    private static readonly Guid Shoulders = new("00000000-0000-0000-0000-0000000000c3");
    private static readonly Guid Quads = new("00000000-0000-0000-0000-0000000000c4");

    [Fact]
    public void Allocate_LandsTheWeekOnTheTargetVolume()
    {
        // Četiri vežbe za grudi po tri serije daju 12; cilj je 16.
        var slots = Isolations(4, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 16, mrv: 22) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(16, TotalFor(slots, sets, Chest));
    }

    [Fact]
    public void Allocate_SpreadsTheCorrectionInsteadOfLoadingTheFirstDay()
    {
        // Ista nedelja kao iznad. Pet-pet-tri-tri pogađa isti zbir, ali ceo ispravak
        // sručuje na prvi dan i ostavlja poslednji ispod — zato ravnomerna podela.
        var slots = Isolations(4, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 16, mrv: 22) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.All(slots, slot => Assert.Equal(4, sets[slot.Id]));
    }

    [Fact]
    public void Allocate_NeverDriftsFurtherThanAllowedFromThePrescription()
    {
        // Cilj koji se ne može dostići ne sme da naduva trening: propis ostaje sidro.
        var slots = Isolations(2, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 40, mrv: 60) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.All(
            slots,
            slot => Assert.Equal(3 + WeeklySetAllocation.MaxDriftFromPrescription, sets[slot.Id]));
    }

    [Fact]
    public void Allocate_NeverPutsMoreThanSixSetsOnOneExercise()
    {
        // Propis pet plus dozvoljeno lutanje daje sedam, a preko šest serije prestaju da
        // dodaju stimulus i dodaju samo zamor.
        var slots = Isolations(1, prescribedSets: 5, Chest);
        var targets = new[] { Target(Chest, mav: 40, mrv: 60) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(WeeklySetAllocation.MaxSetsPerExercise, sets[slots[0].Id]);
    }

    [Fact]
    public void Allocate_NeverDropsAnExerciseBelowTwoSets()
    {
        var slots = Isolations(1, prescribedSets: 4, Chest);
        var targets = new[] { Target(Chest, mav: 1, mrv: 2) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(WeeklySetAllocation.MinSetsPerExercise, sets[slots[0].Id]);
    }

    [Fact]
    public void Allocate_DoesNotRaiseAPrescriptionThatAlreadySitsBelowTheFloor()
    {
        // Deload nosi jednu seriju. Balansiranje volumena sme da odustane od pomoći,
        // ali ne sme da pregazi odluku donetu na drugom mestu.
        var slots = Isolations(1, prescribedSets: 1, Chest);
        var targets = new[] { Target(Chest, mav: 1, mrv: 1) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(1, sets[slots[0].Id]);
    }

    [Fact]
    public void Allocate_AddsVolumeThroughIsolationRatherThanCompoundWork()
    {
        // Grudima nedostaje jedna serija, a ramena i triceps su tačno na cilju. Serija
        // potiska bi grudi popravila, ali bi ramena i triceps povukla preko njihovog —
        // serija raspona to ne radi, pa je ona tačan izbor.
        var bench = new ExerciseSetSlot(
            SlotId(1),
            3,
            [new MuscleLoad(Chest, 1.0m), new MuscleLoad(Triceps, 0.5m), new MuscleLoad(Shoulders, 0.5m)]);
        var fly = new ExerciseSetSlot(SlotId(2), 3, [new MuscleLoad(Chest, 1.0m)]);

        var sets = WeeklySetAllocation.Allocate(
            [bench, fly],
            [
                Target(Chest, mav: 7, mrv: 22),
                Target(Triceps, mav: 1.5m, mrv: 18),
                Target(Shoulders, mav: 1.5m, mrv: 26)
            ]);

        Assert.Equal(3, sets[bench.Id]);
        Assert.Equal(4, sets[fly.Id]);
    }

    [Fact]
    public void Allocate_StopsAtTheRecoveryCeilingInsteadOfReachingTheTarget()
    {
        // Promašen cilj košta napredak; probijen MRV košta nedelje koje dolaze.
        var slots = Isolations(1, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 10, mrv: 4) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(4, sets[slots[0].Id]);
    }

    [Fact]
    public void Allocate_RaisesWhatRemainsOfTheWeekAfterAShortSession()
    {
        // Nedelja traži osam serija za grudi, a prvi trening je upisao samo dve.
        // Preostalom treningu ostaje šest.
        var remaining = Isolations(1, prescribedSets: 4, Chest);
        var targets = new[] { Target(Chest, mav: 8, mrv: 12) };

        var sets = WeeklySetAllocation.Allocate(
            remaining,
            targets,
            Banked(Chest, 2m),
            Banked(Chest, 2m));

        Assert.Equal(6, sets[remaining[0].Id]);
    }

    [Fact]
    public void Allocate_LowersWhatRemainsOfTheWeekAfterAnOvershoot()
    {
        // Prilagođavanje ide u oba smera: deset upisanih serija na cilj od osam znači da
        // preostali trening dobija najmanje što sme, ne svoj propis.
        var remaining = Isolations(1, prescribedSets: 4, Chest);
        var targets = new[] { Target(Chest, mav: 8, mrv: 12) };

        var sets = WeeklySetAllocation.Allocate(
            remaining,
            targets,
            Banked(Chest, 10m),
            Banked(Chest, 10m));

        Assert.Equal(2, sets[remaining[0].Id]);
    }

    [Fact]
    public void Allocate_DoesNotRewardAWeekOfSetsFarFromFailureWithMoreSets()
    {
        // Deset serija na RIR 5: stimulusa nula, ali je oporavak potrošen. Da se gleda samo
        // stimulativni zbir, sistem bi na već iscrpljenu nedelju dosuo još šest serija.
        var remaining = Isolations(1, prescribedSets: 4, Chest);
        var targets = new[] { Target(Chest, mav: 8, mrv: 10) };

        var sets = WeeklySetAllocation.Allocate(
            remaining,
            targets,
            Banked(Chest, 0m),
            Banked(Chest, 10m));

        Assert.Equal(2, sets[remaining[0].Id]);
    }

    [Fact]
    public void Allocate_BreaksTiesInTheOrderItWasGiven()
    {
        // Jedna serija nedostaje, dve vežbe je mogu primiti podjednako dobro. Izbor mora
        // biti isti pri svakom pokretanju, inače bi se plan menjao sam od sebe.
        var slots = Isolations(2, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 7, mrv: 22) };

        var sets = WeeklySetAllocation.Allocate(slots, targets);

        Assert.Equal(4, sets[slots[0].Id]);
        Assert.Equal(3, sets[slots[1].Id]);
    }

    [Fact]
    public void Allocate_DependsOnlyOnThePrescriptionAndWhatTheWeekBanked()
    {
        // Isti ulaz, isti izlaz: preračun posle svakog treninga konvergira umesto da se
        // nadograđuje na sopstveni prethodni odgovor.
        var slots = Isolations(3, prescribedSets: 3, Chest);
        var targets = new[] { Target(Chest, mav: 13, mrv: 22) };

        var first = WeeklySetAllocation.Allocate(slots, targets, Banked(Chest, 4m), Banked(Chest, 5m));
        var second = WeeklySetAllocation.Allocate(slots, targets, Banked(Chest, 4m), Banked(Chest, 5m));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Allocate_LeavesThePrescriptionAloneWhenNoMuscleHasLimits()
    {
        var slots = Isolations(3, prescribedSets: 4, Chest);

        var sets = WeeklySetAllocation.Allocate(slots, []);

        Assert.All(slots, slot => Assert.Equal(4, sets[slot.Id]));
    }

    [Fact]
    public void Allocate_HandlesAWeekWithNothingToPlan()
    {
        var sets = WeeklySetAllocation.Allocate([], [Target(Chest, mav: 16, mrv: 22)]);

        Assert.Empty(sets);
    }

    [Fact]
    public void Allocate_PullsOneMuscleUpAndAnotherDownInTheSameWeek()
    {
        // Nedelja u kojoj su grudi ispod cilja, a kvadricepsi iznad njega: predlog mora
        // da se pomeri u oba smera odjednom, a ne da bira jedan mišić.
        var chestWork = Isolations(2, prescribedSets: 3, Chest);
        var quadWork = Isolations(2, prescribedSets: 5, Quads, startIndex: 3);
        var slots = chestWork.Concat(quadWork).ToList();

        var sets = WeeklySetAllocation.Allocate(
            slots,
            [Target(Chest, mav: 10, mrv: 22), Target(Quads, mav: 6, mrv: 20)]);

        Assert.Equal(10, TotalFor(chestWork, sets, Chest));
        Assert.Equal(6, TotalFor(quadWork, sets, Quads));
    }

    // --- helpers --------------------------------------------------------------

    private static Guid SlotId(int index)
    {
        return new Guid($"00000000-0000-0000-0000-{index:D12}");
    }

    private static List<ExerciseSetSlot> Isolations(
        int count,
        int prescribedSets,
        Guid muscleGroupId,
        int startIndex = 1)
    {
        return Enumerable
            .Range(startIndex, count)
            .Select(index => new ExerciseSetSlot(
                SlotId(index),
                prescribedSets,
                [new MuscleLoad(muscleGroupId, 1.0m)]))
            .ToList();
    }

    private static MuscleVolumeTarget Target(Guid muscleGroupId, decimal mav, decimal mrv)
    {
        return new MuscleVolumeTarget(muscleGroupId, mav, mrv);
    }

    private static Dictionary<Guid, decimal> Banked(Guid muscleGroupId, decimal sets)
    {
        return new Dictionary<Guid, decimal> { [muscleGroupId] = sets };
    }

    private static decimal TotalFor(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyDictionary<Guid, int> sets,
        Guid muscleGroupId)
    {
        return slots.Sum(slot => slot.Muscles
            .Where(muscle => muscle.MuscleGroupId == muscleGroupId)
            .Sum(muscle => muscle.Contribution * sets[slot.Id]));
    }
}
