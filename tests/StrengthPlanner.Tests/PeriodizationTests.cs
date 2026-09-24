using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Priručnik: <i>"Ne možeš isto trenirati svake nedelje i očekivati da napreduješ."</i>
/// Ovi testovi drže tri rasporeda razdvojena i unutar bezbednih granica.
/// </summary>
public class PeriodizationTests
{
    // Hipertrofija: 8-12 ponavljanja, RIR 1. Srednji nivo: 4 serije.
    private const int HypertrophyMin = 8;
    private const int HypertrophyMax = 12;
    private const int HypertrophyRir = 1;

    // Snaga: 3-6 ponavljanja, RIR 2.
    private const int StrengthMin = 3;
    private const int StrengthMax = 6;
    private const int StrengthRir = 2;

    private const int Sets = 4;

    private static IReadOnlyList<WeekPrescription> Hypertrophy(PeriodizationModel model) =>
        Periodization.ForBlock(model, HypertrophyMin, HypertrophyMax, HypertrophyRir, Sets);

    private static IReadOnlyList<WeekPrescription> Strength(PeriodizationModel model) =>
        Periodization.ForBlock(model, StrengthMin, StrengthMax, StrengthRir, Sets);

    [Theory]
    [InlineData(PeriodizationModel.Flat, 4)]
    [InlineData(PeriodizationModel.Linear, 6)]
    [InlineData(PeriodizationModel.Inverse, 6)]
    public void DurationWeeks_DependsOnTheModel(PeriodizationModel model, int expected)
    {
        Assert.Equal(expected, Periodization.DurationWeeks(model));
        Assert.Equal(expected, Periodization.ForBlock(model, 8, 12, 1, 4).Count);
    }

    [Fact]
    public void Flat_KeepsExactlyTheBehaviourTheSystemHadBeforeModelsExisted()
    {
        // Ravan blok je podrazumevani; ako se ovde nešto pomeri, promenili su se planovi
        // svih zatečenih korisnika.
        var weeks = Hypertrophy(PeriodizationModel.Flat);

        Assert.Equal(4, weeks.Count);

        foreach (var week in weeks.Take(3))
        {
            Assert.False(week.IsDeload);
            Assert.Equal(Sets, week.Sets);
            Assert.Equal(HypertrophyMin, week.RepRangeMin);
            Assert.Equal(HypertrophyMax, week.RepRangeMax);
            Assert.Equal(HypertrophyRir, week.TargetRir);
        }

        var deload = weeks[3];
        Assert.True(deload.IsDeload);
        Assert.Equal(2, deload.Sets);
        Assert.Equal(HypertrophyMin, deload.RepRangeMin);
        Assert.Equal(HypertrophyMax, deload.RepRangeMax);
        Assert.Equal(HypertrophyRir, deload.TargetRir);
    }

    [Fact]
    public void Linear_StartsWithVolumeAndEndsWithIntensity()
    {
        var weeks = Hypertrophy(PeriodizationModel.Linear);

        // Prva nedelja: lakše serije i više serija. Ponavljanja se ne mogu dodati —
        // hipertrofijski opseg već stoji na Epley granici — pa pomeraj koji granica
        // pojede nedelja dobija u seriji: +1 iz oblika, +1 iz granice.
        //
        // Ranije je ovde pisalo 11-12: prozor od dva ponavljanja koji je donju granicu
        // DIGAO za tri, dakle nedelju napravio težom, i to u fazi volumena.
        Assert.Equal(HypertrophyMin, weeks[0].RepRangeMin);
        Assert.Equal(HypertrophyMax, weeks[0].RepRangeMax);
        Assert.Equal(HypertrophyRir + 1, weeks[0].TargetRir);
        Assert.Equal(Sets + 1 + Periodization.CappedShiftSetBonus, weeks[0].Sets);

        // Peta nedelja: manje ponavljanja i manje serija.
        Assert.Equal(6, weeks[4].RepRangeMin);
        Assert.Equal(10, weeks[4].RepRangeMax);
        Assert.Equal(Sets - 1, weeks[4].Sets);

        Assert.True(weeks[5].IsDeload);
    }

    /// <summary>
    /// Serija iznad Epley granice ne daje procenu 1RM-a, a tri stvari je čitaju: trend
    /// snage, prepoznavanje rekorda i signal umora. Nedelja volumena iznad granice bi
    /// izgledala uredno, a sistem bi u njoj prestao da meri.
    /// </summary>
    [Fact]
    public void RepRange_NeverExceedsTheEpleyCap()
    {
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            foreach (var weeks in new[] { Hypertrophy(model), Strength(model) })
            {
                Assert.All(weeks, week =>
                    Assert.True(
                        week.RepRangeMax <= TrainingConstants.EpleyRepCap,
                        $"{model} nedelja {week.WeekNumber}: {week.RepRangeMax} ponavljanja "
                        + $"prelazi Epley granicu {TrainingConstants.EpleyRepCap}."));
            }
        }
    }

    /// <summary>
    /// Umor se meri kao manjak u odnosu na ciljni RIR. Ispod nule manjka nema — ponavljanja
    /// u rezervi ne idu u minus — pa bi nedelja propisana do otkaza tiho izgubila najteži
    /// član ocene umora i nikada ne bi mogla da pokrene raniji deload.
    /// </summary>
    [Fact]
    public void TargetRir_NeverDropsToFailure()
    {
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            foreach (var weeks in new[] { Hypertrophy(model), Strength(model) })
            {
                Assert.All(weeks, week =>
                    Assert.True(
                        week.TargetRir >= 1,
                        $"{model} nedelja {week.WeekNumber}: ciljni RIR {week.TargetRir}."));
            }
        }
    }

    [Fact]
    public void BaseSetsFrom_InvertsEveryTrainingWeek()
    {
        // Deload logika iz zatečenog plana mora da izvede polazni broj serija bloka —
        // profil se ne čita, jer korisnik koji je usred bloka promenio nivo iskustva ne
        // sme time da promeni oblik već napravljenog plana.
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            // Oba cilja i fiksan broj ponavljanja: pomeraj serija više nije konstanta
            // oblika — nedelja kojoj je Epley granica pojela ponavljanja nosi seriju više —
            // pa se osnova ne može izvesti bez opsega iz koga je nedelja propisana.
            foreach (var (min, max, rir) in new[]
                     {
                         (HypertrophyMin, HypertrophyMax, HypertrophyRir),
                         (StrengthMin, StrengthMax, StrengthRir),
                         (5, 5, HypertrophyRir)
                     })
            {
                var weeks = Periodization.ForBlock(model, min, max, rir, Sets);

                foreach (var week in weeks.Where(week => !week.IsDeload))
                {
                    Assert.Equal(
                        Sets,
                        Periodization.BaseSetsFrom(model, week.WeekNumber, week.Sets, max));
                }

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Periodization.BaseSetsFrom(
                        model,
                        weeks[^1].WeekNumber,
                        weeks[^1].Sets,
                        max));
            }
        }
    }

    /// <summary>
    /// Nedelja koja nosi osnovu postoji u svakom modelu, i to je jedina nedelja iz koje se
    /// polazni broj serija sme da PROCITA. Zatecen red nosi broj koji je propisala starija
    /// verzija pravila, pa bi iz njega izvedena osnova bila nedelja koju blok nikada nije
    /// imao (izmereno nad dev bazom: 392 od 440 redova u takvim nedeljama upisala je
    /// starija verzija pravila, a 168 njih nosi prozor 11-15, koji ovaj kod ne ume da
    /// napravi).
    /// </summary>
    [Theory]
    [InlineData(PeriodizationModel.Flat, 1)]
    [InlineData(PeriodizationModel.Linear, 3)]
    [InlineData(PeriodizationModel.Inverse, 3)]
    public void TheBaseWeek_CarriesTheBasePrescriptionItself(PeriodizationModel model, int expected)
    {
        Assert.Equal(expected, Periodization.BaseWeekNumber(model));

        foreach (var (min, max, rir) in new[]
                 {
                     (HypertrophyMin, HypertrophyMax, HypertrophyRir),
                     (StrengthMin, StrengthMax, StrengthRir),
                     (5, 5, HypertrophyRir)
                 })
        {
            var week = Periodization.ForWeek(model, expected, min, max, rir, Sets);

            Assert.False(week.IsDeload);
            Assert.Equal(min, week.RepRangeMin);
            Assert.Equal(max, week.RepRangeMax);
            Assert.Equal(rir, week.TargetRir);
            Assert.Equal(Sets, week.Sets);
        }
    }

    /// <summary>
    /// A rezerva se ne pretvara da može sve: obrtanje pomeraja ne može da razlikuje dve
    /// osnove koje daju isti broj serija. Osnova 2 i osnova 3 u nedelji koja skida seriju
    /// obe propisuju dve (jer <see cref="Periodization.MinSets"/> secka), pa izvođenje
    /// vraća tri i za jednu i za drugu. Posledica je deload od dve serije tamo gde bi
    /// trebalo jedna — zato se osnova čita iz osnovne nedelje kad je god dostupna.
    /// </summary>
    [Fact]
    public void RecoveringTheBase_CannotUndoTheMinimumSetClamp()
    {
        var fromTwo = Periodization.ForWeek(PeriodizationModel.Linear, 5, HypertrophyMin, HypertrophyMax, HypertrophyRir, 2);
        var fromThree = Periodization.ForWeek(PeriodizationModel.Linear, 5, HypertrophyMin, HypertrophyMax, HypertrophyRir, 3);

        Assert.Equal(Periodization.MinSets, fromTwo.Sets);
        Assert.Equal(fromTwo.Sets, fromThree.Sets);

        var recovered = Periodization.BaseSetsFrom(PeriodizationModel.Linear, 5, fromTwo.Sets, HypertrophyMax);

        Assert.Equal(3, recovered);
        Assert.NotEqual(2, recovered);
    }

    /// <summary>
    /// Epley granica je granica MERENJA, pa pomera prozor umesto da ga sužava: nedelja
    /// zadržava širinu opsega iz koga je propisana. Bez toga je faza volumena
    /// hipertrofije ispadala kao 11-12, gde dupla progresija nema po čemu da raste.
    /// </summary>
    [Fact]
    public void TheEpleyCap_MovesTheRepWindow_InsteadOfNarrowingIt()
    {
        foreach (var model in new[] { PeriodizationModel.Linear, PeriodizationModel.Inverse })
        {
            // Hipertrofija stoji na samoj granici, pa je ona jedina koja tu i seče:
            // svaka nedelja zadržava širinu od četiri ponavljanja.
            Assert.All(Hypertrophy(model), week => Assert.Equal(
                HypertrophyMax - HypertrophyMin,
                week.RepRangeMax - week.RepRangeMin));

            // Snaga prozor sužava, ali samo na donjem kraju — i tada stoji tačno na podu
            // od tri ponavljanja, što je odluka, a ne posledica merenja.
            Assert.All(Strength(model), week => Assert.True(
                week.RepRangeMax - week.RepRangeMin == StrengthMax - StrengthMin
                || week.RepRangeMin == Periodization.MinReps,
                $"{model} nedelja {week.WeekNumber}: {week.RepRangeMin}-{week.RepRangeMax}"));
        }
    }

    /// <summary>
    /// Donja granica je trenažna odluka, ne merna, pa ona sme da suži prozor: ispod tri
    /// ponavljanja blok više nije ono što piše da jeste.
    /// </summary>
    [Fact]
    public void TheThreeRepFloor_StillNarrowsTheWindow()
    {
        var intensityWeek = Strength(PeriodizationModel.Linear)[4];

        Assert.Equal(Periodization.MinReps, intensityWeek.RepRangeMin);
        Assert.Equal(4, intensityWeek.RepRangeMax);
    }

    /// <summary>
    /// Kada granica pojede pomeraj ponavljanja, nedelja ga dobija u seriji. Blok snage ne
    /// dodiruje granicu, pa tamo nema ni bonusa — isti broj serija kao pre ove izmene.
    /// </summary>
    [Fact]
    public void ASwallowedRepShift_ComesBackAsASet()
    {
        var hypertrophy = Hypertrophy(PeriodizationModel.Inverse)
            .Where(week => !week.IsDeload)
            .Select(week => week.Sets);
        var strength = Strength(PeriodizationModel.Inverse)
            .Where(week => !week.IsDeload)
            .Select(week => week.Sets);

        Assert.Equal(new[] { 3, 3, 4, 5, 6 }, hypertrophy);
        Assert.Equal(new[] { 3, 3, 4, 4, 5 }, strength);
    }

    [Fact]
    public void Inverse_IsTheMirrorOfLinear()
    {
        var linear = Hypertrophy(PeriodizationModel.Linear);
        var inverse = Hypertrophy(PeriodizationModel.Inverse);

        // Obrnut model počinje tamo gde linearni završava i obrnuto.
        Assert.Equal(linear[4].RepRangeMin, inverse[0].RepRangeMin);
        Assert.Equal(linear[4].RepRangeMax, inverse[0].RepRangeMax);
        Assert.Equal(linear[0].RepRangeMin, inverse[4].RepRangeMin);
        Assert.Equal(linear[0].RepRangeMax, inverse[4].RepRangeMax);
    }

    [Theory]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void PeriodizedBlocks_LowerTheRirAsTheBlockGoesOn(PeriodizationModel model)
    {
        // Zamor raste kroz blok, pa se serije vode sve bliže otkazu — do deload-a.
        // Kod snage ima prostora za pravi pad; kod hipertrofije osnovni RIR je već 1, pa
        // se pad zaustavlja na donjoj granici i intenzitet dalje nose ponavljanja.
        var training = Strength(model).Where(week => !week.IsDeload).ToList();
        Assert.True(training[^1].TargetRir < training[0].TargetRir);

        training = Hypertrophy(model).Where(week => !week.IsDeload).ToList();

        for (var index = 1; index < training.Count; index++)
        {
            Assert.True(
                training[index].TargetRir <= training[index - 1].TargetRir,
                $"Nedelja {training[index].WeekNumber}: RIR je porastao.");
        }
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void EveryBlock_HasExactlyOneDeloadAndItIsLast(PeriodizationModel model)
    {
        var weeks = Hypertrophy(model);

        Assert.Single(weeks.Where(week => week.IsDeload));
        Assert.True(weeks[^1].IsDeload);
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void EveryWeek_StaysInsideSafeBounds(PeriodizationModel model)
    {
        foreach (var weeks in new[] { Hypertrophy(model), Strength(model) })
        {
            foreach (var week in weeks)
            {
                Assert.InRange(week.RepRangeMin, Periodization.MinReps, Periodization.MaxReps);
                Assert.InRange(week.RepRangeMax, week.RepRangeMin + 1, Periodization.MaxReps);
                Assert.InRange(week.TargetRir, Periodization.MinRir, Periodization.MaxRir);
                Assert.True(week.Sets >= 1, $"Nedelja {week.WeekNumber}: {week.Sets} serija.");
            }
        }
    }

    [Fact]
    public void Strength_DoesNotDropBelowThreeReps_AndLeansOnRirInstead()
    {
        // Blok snage već stoji na 3-6 ponavljanja; niže od tri se ne ide, pa fazu
        // intenziteta nosi RIR, a ne još kraći opseg.
        var weeks = Strength(PeriodizationModel.Linear);
        var intensityWeek = weeks[4];

        Assert.Equal(Periodization.MinReps, intensityWeek.RepRangeMin);
        Assert.True(intensityWeek.TargetRir < StrengthRir);
    }

    [Fact]
    public void EveryTrainingWeekOfAPeriodizedBlock_HasItsOwnPrescription()
    {
        // Dve uzastopne nedelje sa istim propisom znače da model tu nedelju ne koristi.
        foreach (var model in new[] { PeriodizationModel.Linear, PeriodizationModel.Inverse })
        {
            foreach (var weeks in new[] { Hypertrophy(model), Strength(model) })
            {
                var training = weeks.Where(week => !week.IsDeload).ToList();

                for (var index = 1; index < training.Count; index++)
                {
                    var previous = training[index - 1];
                    var current = training[index];

                    Assert.False(
                        previous.Sets == current.Sets
                        && previous.RepRangeMin == current.RepRangeMin
                        && previous.RepRangeMax == current.RepRangeMax
                        && previous.TargetRir == current.TargetRir,
                        $"{model}: nedelje {previous.WeekNumber} i {current.WeekNumber} su iste.");
                }
            }
        }
    }

    [Fact]
    public void DeloadSets_HalveTheWeekButNeverFallBelowOne()
    {
        Assert.Equal(2, Periodization.DeloadSets(4));
        Assert.Equal(2, Periodization.DeloadSets(3));
        Assert.Equal(1, Periodization.DeloadSets(1));
    }

    [Fact]
    public void DeloadWeek_KeepsTheGoalRepRangeAndRir()
    {
        // Rasterećenje nosi opterećenje (90% stvarnog) i polovinu serija; menjanje i
        // opsega bi promenilo i sam pokret, a ne samo njegovu težinu.
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            var deload = Hypertrophy(model)[^1];

            Assert.Equal(HypertrophyMin, deload.RepRangeMin);
            Assert.Equal(HypertrophyMax, deload.RepRangeMax);
            Assert.Equal(HypertrophyRir, deload.TargetRir);
        }
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void ForWeek_RejectsAWeekOutsideTheBlock(PeriodizationModel model)
    {
        var duration = Periodization.DurationWeeks(model);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Periodization.ForWeek(model, 0, 8, 12, 1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Periodization.ForWeek(model, duration + 1, 8, 12, 1, 4));
    }

    [Fact]
    public void ForWeek_NumbersWeeksFromOneInOrder()
    {
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            var weeks = Hypertrophy(model);

            Assert.Equal(
                Enumerable.Range(1, weeks.Count),
                weeks.Select(week => week.WeekNumber));
        }
    }

    [Fact]
    public void PeriodizedBlocks_ActuallyDifferFromWeekToWeek()
    {
        // Bez ovoga bi model mogao da se "primeni" a da ne promeni nijedan propis —
        // što je upravo zamerka od koje je ova grana krenula.
        foreach (var model in new[] { PeriodizationModel.Linear, PeriodizationModel.Inverse })
        {
            var distinct = Hypertrophy(model)
                .Select(week => (week.Sets, week.RepRangeMin, week.RepRangeMax, week.TargetRir))
                .Distinct()
                .Count();

            Assert.True(distinct >= 4, $"{model}: samo {distinct} različitih propisa u bloku.");
        }
    }
}
