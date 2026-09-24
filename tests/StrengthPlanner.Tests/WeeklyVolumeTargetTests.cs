using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Balansiranje serija je svaku nedelju gađalo u MAV, pa je brisalo ono što periodizacija
/// jedino i kaže naglas — koliko se rada radi. Izmereno na linearnom hipertrofijskom bloku:
/// propis za grudi je išao 20, 20, 16, 16, 12 serija, a posle balansiranja je svaka nedelja
/// ispadala 16.
///
/// Uz to su granice volumena hipertrofijske granice (MAV je definisan kao volumen koji
/// pokreće rast), pa je blok snage gađao broj koji ne pripada njemu.
/// </summary>
public class WeeklyVolumeTargetTests
{
    // Grudi iz kataloga: MEV 10, MAV 16, MRV 22.
    private static readonly VolumeLandmarkValues Chest = new(Mev: 10, Mav: 16, Mrv: 22);

    [Fact]
    public void AHypertrophyBlock_AimsAtMav()
    {
        Assert.Equal(16m, WeeklyVolumeTarget.ForGoal(Goal.Hypertrophy, Chest));
    }

    /// <summary>
    /// Blok snage cilja na pola puta između MEV-a i MAV-a. Izvedeno iz granica koje
    /// korisnik ionako uči, a ne novim množiocem: serije su teže i skuplje po oporavku,
    /// pa ih ide manje.
    /// </summary>
    [Fact]
    public void AStrengthBlock_AimsLower()
    {
        Assert.Equal(13m, WeeklyVolumeTarget.ForGoal(Goal.Strength, Chest));
    }

    /// <summary>
    /// Ravna nedelja daje odnos jedan, dakle tačno MAV — zatečeni ravni blokovi se ne
    /// menjaju. To je i uslov da ova izmena bude hirurška.
    /// </summary>
    [Theory]
    [InlineData(16, 16)]
    [InlineData(12, 12)]
    [InlineData(1, 1)]
    public void AWeekThatPrescribesTheBaseVolume_AimsAtMav(int prescribed, int baseVolume)
    {
        Assert.Equal(
            16m,
            WeeklyVolumeTarget.ForWeek(Goal.Hypertrophy, Chest, prescribed, baseVolume));
    }

    /// <summary>
    /// Cilj se pomera onoliko koliko je periodizacija pomerila propis. Brojevi su iz
    /// stvarnog linearnog bloka: četiri vežbe koje grudima daju punu seriju, osnova četiri
    /// serije, pa nedelje volumena nose 20 a nedelja intenziteta 12.
    /// </summary>
    [Theory]
    [InlineData(20, 16, 20)]
    [InlineData(12, 16, 12)]
    [InlineData(24, 16, 22)]
    public void TheTargetFollowsThePrescription(int prescribed, int baseVolume, decimal expected)
    {
        Assert.Equal(
            expected,
            WeeklyVolumeTarget.ForWeek(Goal.Hypertrophy, Chest, prescribed, baseVolume));
    }

    /// <summary>
    /// Granice ostaju granice: cilj nikad ne izađe iz pojasa. Ispod MEV-a nedelja ne bi
    /// održavala, iznad MRV-a se ne bi oporavljala.
    /// </summary>
    [Fact]
    public void TheTargetStaysInsideTheBand()
    {
        foreach (var goal in Enum.GetValues<Goal>())
        {
            for (var prescribed = 0; prescribed <= 80; prescribed++)
            {
                var target = WeeklyVolumeTarget.ForWeek(goal, Chest, prescribed, 16);

                Assert.InRange(target, Chest.Mev, Chest.Mrv);
            }
        }
    }

    /// <summary>
    /// Kada se polazni volumen ne zna (osnovna nedelja je i sama postala deload, ili mišić
    /// nije u planu), nedelja gađa cilj bloka nepomeren — a ne izmišljen odnos.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void WithoutAKnownBaseVolume_TheWeekAimsAtTheBlockTarget(int baseVolume)
    {
        Assert.Equal(
            16m,
            WeeklyVolumeTarget.ForWeek(Goal.Hypertrophy, Chest, prescribedSets: 20, baseSets: baseVolume));
        Assert.Equal(
            13m,
            WeeklyVolumeTarget.ForWeek(Goal.Strength, Chest, prescribedSets: 20, baseSets: baseVolume));
    }

    /// <summary>
    /// Zašto ovo uopšte postoji: sa istim ciljem u svakoj nedelji alokator spljošti talas.
    /// Ovo je merenje iz nalaza, sada kao test — prvi red pada na starom pravilu.
    /// </summary>
    [Fact]
    public void TheWaveSurvivesBalancing_WhereItUsedToBeFlattened()
    {
        var chestId = new Guid("00000000-0000-0000-0000-0000000000c1");
        var weeks = Periodization.ForBlock(PeriodizationModel.Linear, 8, 12, 1, baseSets: 4)
            .Where(week => !week.IsDeload)
            .ToList();
        var baseVolume = 4 * Periodization
            .ForWeek(PeriodizationModel.Linear, Periodization.BaseWeekNumber(PeriodizationModel.Linear), 8, 12, 1, 4)
            .Sets;

        var moving = new List<int>();
        var fixedAtMav = new List<int>();

        foreach (var week in weeks)
        {
            // Četiri vežbe, svaka daje grudima punu seriju.
            var slots = Enumerable.Range(0, 4)
                .Select(index => new ExerciseSetSlot(
                    new Guid($"00000000-0000-0000-0000-00000000{index:00}01"),
                    week.Sets,
                    [new MuscleLoad(chestId, 1m)]))
                .ToList();
            var prescribed = slots.Sum(slot => slot.PrescribedSets);

            var weekTarget = WeeklyVolumeTarget.ForWeek(Goal.Hypertrophy, Chest, prescribed, baseVolume);

            moving.Add(Total(slots, WeeklySetAllocation.Allocate(
                slots,
                [new MuscleVolumeTarget(chestId, weekTarget, Chest.Mrv)])));
            fixedAtMav.Add(Total(slots, WeeklySetAllocation.Allocate(
                slots,
                [new MuscleVolumeTarget(chestId, Chest.Mav, Chest.Mrv)])));
        }

        // Propis te iste petlje: 24, 24, 16, 16, 12 (nedelje volumena nose i seriju koju im
        // Epley granica vraća, vidi rep-window-and-fixed-reps.md).
        Assert.Equal(new[] { 22, 22, 16, 16, 12 }, moving);

        // Staro pravilo: svaka nedelja u MAV, talasa nema.
        Assert.Equal(new[] { 16, 16, 16, 16, 16 }, fixedAtMav);
    }

    /// <summary>
    /// Blok snage dobija manje serija od hipertrofijskog uz iste granice i isti šablon.
    /// </summary>
    [Fact]
    public void AStrengthBlock_GetsFewerSetsThanAHypertrophyBlock()
    {
        var chestId = new Guid("00000000-0000-0000-0000-0000000000c1");
        var slots = Enumerable.Range(0, 4)
            .Select(index => new ExerciseSetSlot(
                new Guid($"00000000-0000-0000-0000-00000000{index:00}01"),
                4,
                [new MuscleLoad(chestId, 1m)]))
            .ToList();

        var hypertrophy = Total(slots, WeeklySetAllocation.Allocate(
            slots,
            [new MuscleVolumeTarget(chestId, WeeklyVolumeTarget.ForGoal(Goal.Hypertrophy, Chest), Chest.Mrv)]));
        var strength = Total(slots, WeeklySetAllocation.Allocate(
            slots,
            [new MuscleVolumeTarget(chestId, WeeklyVolumeTarget.ForGoal(Goal.Strength, Chest), Chest.Mrv)]));

        Assert.Equal(16, hypertrophy);
        Assert.Equal(13, strength);
        Assert.True(strength < hypertrophy);
    }

    private static int Total(
        IReadOnlyList<ExerciseSetSlot> slots,
        IReadOnlyDictionary<Guid, int> allocated)
    {
        return slots.Sum(slot => allocated[slot.Id]);
    }
}
