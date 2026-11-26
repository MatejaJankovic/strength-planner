using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Treći cilj, održavanje (R20.4): blok koji čuva ono što je prethodni izgradio, sa najmanje treninga koji to radi.
/// Svako pravilo koje čita cilj dobilo je treću vrednost, a ovde se beleži šta ono sa njom radi i zašto: volumen oko
/// MEV-a, težine se ne podižu, granice se iz takvog bloka ne uče, blok je uvek ravan i plan održavanja ponavlja
/// isti blok. Izvor za smisao celine: Spiering i sar. 2021 (intenzitet je promenljiva koja odlučuje, volumen i
/// učestalost mogu mnogo da se smanje) i Bickel i sar. 2011 (trećina i devetina doze održavaju mišićnu masu), oba
/// čitana preko sažetaka, ne celog teksta.
/// </summary>
public class MaintenanceGoalTests
{
    private readonly ProgressionEngine _engine = new();

    // ---- propis -------------------------------------------------------------------------------------------------

    [Fact]
    public void TheBlockPrescribes_SixToTenReps_WithThreeInReserve()
    {
        Assert.Equal(new GoalPrescription(6, 10, 3), GoalPrescriptions.ForGoal(Goal.Maintenance));
    }

    /// <summary>
    /// Težina koju blok održavanja propisuje je ona hipertrofijskog: po Epley-u set vredi ponavljanja plus rezerva,
    /// a 6 + 3 i 10 + 3 su isto što i 8 + 1 i 12 + 1. Dva ponavljanja manje i dva više u rezervi, na istoj šipki.
    /// </summary>
    [Fact]
    public void TheBlockLoadsTheBarLikeAHypertrophyBlock_ForFewerRepsAndMoreReserve()
    {
        var maintenance = GoalPrescriptions.ForGoal(Goal.Maintenance);
        var hypertrophy = GoalPrescriptions.ForGoal(Goal.Hypertrophy);

        Assert.Equal(
            hypertrophy.RepRangeMin + hypertrophy.TargetRir,
            maintenance.RepRangeMin + maintenance.TargetRir);
        Assert.Equal(
            hypertrophy.RepRangeMax + hypertrophy.TargetRir,
            maintenance.RepRangeMax + maintenance.TargetRir);
        Assert.True(maintenance.RepRangeMax < hypertrophy.RepRangeMax);
        Assert.True(maintenance.TargetRir > hypertrophy.TargetRir);
    }

    /// <summary>
    /// Svi složeni nose raspon bloka, kao u hipertrofiji (nema "glavnih dizanja" koja balansiranje ne sme da pomeri),
    /// a izolacija zadržava svoj. Rezerva je rezerva bloka.
    /// </summary>
    [Theory]
    [InlineData(ExerciseType.Compound, true, 6, 10)]
    [InlineData(ExerciseType.Compound, false, 6, 10)]
    [InlineData(ExerciseType.Isolation, true, 10, 20)]
    public void EveryExercise_CarriesTheBlocksReserve_AndCompoundsItsRange(
        ExerciseType type,
        bool suitsLowReps,
        int expectedMin,
        int expectedMax)
    {
        var prescription = GoalPrescriptions.ForExercise(Goal.Maintenance, type, suitsLowReps);

        Assert.Equal((expectedMin, expectedMax, 3), (prescription.RepRangeMin, prescription.RepRangeMax, prescription.TargetRir));
        Assert.False(GoalPrescriptions.IsStrengthLift(Goal.Maintenance, type, suitsLowReps));
        Assert.False(GoalPrescriptions.IsMainLift(Goal.Maintenance, type, prescription.RepRangeMax));
    }

    // ---- volumen ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 16, 22)]
    [InlineData(5, 9, 15)]
    [InlineData(8, 12, 20)]
    public void TheWeeklyTarget_IsTheMev(int mev, int mav, int mrv)
    {
        var landmarks = new VolumeLandmarkValues(mev, mav, mrv);

        Assert.Equal(mev, WeeklyVolumeTarget.ForGoal(Goal.Maintenance, landmarks));
        // Ravan blok: nedelja je jednaka osnovnoj, pa je odnos jedan i cilj se ne pomera.
        Assert.Equal(mev, WeeklyVolumeTarget.ForWeek(Goal.Maintenance, landmarks, 12m, 12m));
        // Ni ostali ciljevi se nisu pomerili: hipertrofija MAV, snaga sredina između MEV-a i MAV-a.
        Assert.Equal(mav, WeeklyVolumeTarget.ForGoal(Goal.Hypertrophy, landmarks));
        Assert.Equal((mev + mav) / 2m, WeeklyVolumeTarget.ForGoal(Goal.Strength, landmarks));
    }

    /// <summary>
    /// Nedelja bloka održavanja posle balansiranja, na prvom nedeljnom propisu svakog ugrađenog šablona na svakom
    /// nivou (mišići sa MEV-om nula nisu računati: za njih svaka serija je "iznad"). Zapisano tačno: 182 od 274
    /// mišića koje šablon trenira stoji tačno na MEV-u, 28 ispod (šablon nema dovoljno posla za taj mišić, isto kao u
    /// rastućem bloku), 64 iznad, od toga 21 za više od dve serije; još 23 para šablon-mišić uopšte nema vežbu. Razlog iznad je najmanji broj serija po
    /// vežbi (dve): vežba se ne ukida, pa mišić sa mnogo vežbi ili velikim sporednim udelom ne može da siđe na MEV.
    /// Prosek je 7.1 serija po mišiću naspram 10.8 u hipertrofiji (66%).
    /// </summary>
    [Fact]
    public void AMaintenanceWeek_LandsOnMev_WhereTheTemplateCanReachIt()
    {
        var trained = 0;
        var atMev = 0;
        var below = 0;
        var above = 0;
        var aboveByMoreThanTwo = 0;
        var untrained = 0;
        var maintenanceSets = 0m;
        var hypertrophySets = 0m;

        foreach (var week in TemplateWeekSimulation.EveryMaintenanceWeek())
        {
            var hypertrophy = TemplateWeekSimulation.Build(
                week.Template,
                week.Level,
                Goal.Hypertrophy,
                PeriodizationModel.Flat,
                1);

            foreach (var seed in ExerciseCatalog.VolumeLandmarks.Where(seed => seed.Mev > 0))
            {
                var mev = ExperienceProgramming.ScaleLandmarks(
                    new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                    week.Level).Mev;
                var sets = week.Weekly(seed.Muscle);

                if (sets == 0)
                {
                    untrained++;
                    continue;
                }

                trained++;
                maintenanceSets += sets;
                hypertrophySets += hypertrophy.Weekly(seed.Muscle);

                if (sets == mev)
                {
                    atMev++;
                }
                else if (sets < mev)
                {
                    below++;
                }
                else
                {
                    above++;
                    if (sets > mev + 2)
                    {
                        aboveByMoreThanTwo++;
                    }
                }
            }
        }

        Assert.Equal(
            (Trained: 274, AtMev: 182, Below: 28, Above: 64, AboveByMoreThanTwo: 21, Untrained: 23),
            (trained, atMev, below, above, aboveByMoreThanTwo, untrained));
        Assert.True(maintenanceSets < hypertrophySets);
        Assert.Equal(7.1m, Math.Round(maintenanceSets / trained, 1));
        Assert.Equal(10.8m, Math.Round(hypertrophySets / trained, 1));
    }

    // ---- plan i raspored ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void AMaintenancePlan_IsTheSameBlockRepeated(int blocks)
    {
        Assert.Equal(
            Enumerable.Repeat(Goal.Maintenance, blocks),
            MacrocyclePlanner.AlternatingGoals(blocks, Goal.Maintenance));
    }

    [Fact]
    public void TheOtherPlans_StillAlternate()
    {
        Assert.Equal(
            [Goal.Hypertrophy, Goal.Strength, Goal.Hypertrophy],
            MacrocyclePlanner.AlternatingGoals(3, Goal.Hypertrophy));
        Assert.Equal(
            [Goal.Strength, Goal.Hypertrophy],
            MacrocyclePlanner.AlternatingGoals(2, Goal.Strength));
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.LinearRising)]
    [InlineData(PeriodizationModel.Inverse)]
    public void AMaintenanceBlock_IsAlwaysFlat_AndTheOtherGoalsKeepWhatWasAsked(PeriodizationModel requested)
    {
        Assert.Equal(PeriodizationModel.Flat, Periodization.ModelFor(Goal.Maintenance, requested));
        Assert.Equal(requested, Periodization.ModelFor(Goal.Hypertrophy, requested));
        Assert.Equal(requested, Periodization.ModelFor(Goal.Strength, requested));
    }

    // ---- težine se ne guraju ------------------------------------------------------------------------------------

    private static readonly decimal[] Loads = [20m, 47.5m, 82.5m, 100m, 160m];
    private static readonly decimal[] Steps = [1m, 2m, 2.5m, 5m];

    private ProgressionResult Next(decimal load, decimal step, int reps, int rir, bool stepAtTop)
    {
        return _engine.ComputeNext(
            load,
            [new WorkingSet(reps, rir), new WorkingSet(reps, rir), new WorkingSet(reps, rir)],
            targetRir: 3,
            repRangeMin: 6,
            repRangeMax: 10,
            weightStepKg: step,
            stepAtTopOfRange: stepAtTop);
    }

    /// <summary>
    /// Tri serije od 10 uz tri u rezervi, tačno ono što blok traži: hipertrofija bi digla težinu (vrh opsega), održavanje
    /// je drži. 100 kg na šipki sa korakom 2.5 je 102.5 u jednom, 100 u drugom.
    /// </summary>
    [Fact]
    public void ReachingTheTopOnTarget_RaisesTheLoadOnlyWhereTheBlockBuilds()
    {
        Assert.Equal(102.5m, Next(100m, 2.5m, 10, 3, stepAtTop: true).NextWeightKg);

        var held = Next(100m, 2.5m, 10, 3, stepAtTop: false);
        Assert.Equal(100m, held.NextWeightKg);
        Assert.False(held.WeightIncreased);
    }

    /// <summary>
    /// Ono što nije korak ostaje: set mnogo lakši od cilja (deset ponavljanja uz pet u rezervi) i dalje daje težinu
    /// koja vraća napor na cilj, jer to nije napredovanje nego održavanje intenziteta, a vrh opsega nikad ne spušta.
    /// </summary>
    [Fact]
    public void AnEasierSetThanTheTarget_StillRestoresTheEffort_AndTheTopNeverLowersTheLoad()
    {
        var easier = Next(100m, 2.5m, 10, 5, stepAtTop: false);
        var standard = Next(100m, 2.5m, 10, 5, stepAtTop: true);

        Assert.Equal(105m, easier.NextWeightKg);
        Assert.True(easier.NextWeightKg < standard.NextWeightKg);

        // Deset ponavljanja do otkaza uz tri u rezervi u planu: težina ostaje, ne pada zbog vrha opsega.
        Assert.Equal(100m, Next(100m, 2.5m, 10, 0, stepAtTop: false).NextWeightKg);
    }

    /// <summary>
    /// Zastavica menja samo ono što se dešava na vrhu opsega: ispod njega, za svaku težinu, korak i seriju, odgovor
    /// je isti. A na vrhu održavanje nikad ne spušta ispod podignutog i nikad ne ide iznad običnog odgovora.
    /// </summary>
    [Fact]
    public void TheFlag_ChangesOnlyTheTopOfTheRange()
    {
        var heldWhereTheOtherStepped = 0;

        foreach (var load in Loads)
        foreach (var step in Steps)
        foreach (var reps in new[] { 6, 7, 8, 9, 10 })
        foreach (var rir in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var standard = Next(load, step, reps, rir, stepAtTop: true);
            var maintenance = Next(load, step, reps, rir, stepAtTop: false);

            if (reps < 10)
            {
                Assert.Equal(standard, maintenance);
                continue;
            }

            // Tačno na cilju (deset ponavljanja, tri u rezervi) težina stoji za svaki teg i korak; bez ovoga bi zastavica
            // koja se zanemaruje prošla obe granice ispod, jer je običan odgovor tada i gornja i donja granica.
            if (rir == 3)
            {
                Assert.Equal(load, maintenance.NextWeightKg);
            }

            if (maintenance.NextWeightKg < standard.NextWeightKg)
            {
                heldWhereTheOtherStepped++;
            }

            Assert.True(
                maintenance.NextWeightKg >= load,
                $"{load} kg, step {step}, {reps} x RIR {rir}: held at {maintenance.NextWeightKg}");
            Assert.True(
                maintenance.NextWeightKg <= standard.NextWeightKg,
                $"{load} kg, step {step}, {reps} x RIR {rir}: {maintenance.NextWeightKg} above {standard.NextWeightKg}");
        }

        // Da provera nije prazna: na vrhu opsega održavanje stvarno drži tamo gde bi običan blok podigao.
        Assert.True(heldWhereTheOtherStepped > 0);
    }

    [Theory]
    [InlineData(Goal.Strength, true)]
    [InlineData(Goal.Hypertrophy, true)]
    [InlineData(Goal.Maintenance, false)]
    public void OnlyAMaintenanceBlock_TakesTheStepOutOfTheTop(Goal goal, bool steps)
    {
        Assert.Equal(steps, GoalPrescriptions.StepsLoadAtTopOfRange(goal));
    }

    // ---- granice se ne uče --------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Goal.Strength, true)]
    [InlineData(Goal.Hypertrophy, true)]
    [InlineData(Goal.Maintenance, false)]
    public void OnlyTheGrowthGoals_LearnTheVolumeLimits(Goal goal, bool learns)
    {
        Assert.Equal(learns, GoalPrescriptions.LearnsVolumeLimits(goal));
    }

    [Fact]
    public void AnUnknownGoal_LearnsNothing()
    {
        Assert.False(GoalPrescriptions.LearnsVolumeLimits(null));
    }

    /// <summary>
    /// Zašto blok održavanja ćuti. Nedelja na MEV-u je "bar minimum" za pravilo oporavka, pa umor od nečeg trećeg
    /// (posla, sna) spušta MRV; a snaga koja se samo oporavlja posle teškog bloka spušta MEV, a time i sledeći cilj
    /// održavanja. Pravilo zato dobija dokaz koji ne pripada njemu. Oba izmerena nad pravim pravilom učenja.
    /// </summary>
    [Fact]
    public void WhatAWeekAtMevWouldHaveTaught_IsWhyTheBlockLearnsNothing()
    {
        var band = new VolumeLandmarkValues(10, 16, 22);

        // Nedelja na MEV-u, dva ponavljanja teža od cilja: MRV pada za jedan.
        var tired = VolumeAdaptation.Adjust(band, band, new VolumeResponse(10, 10, -2, 0.1m));
        Assert.Equal(new VolumeLandmarkValues(10, 16, 21), tired);

        // Nedelja na MEV-u u kojoj snaga raste dve nedelje zaredom po četiri odsto: MEV pada za jedan.
        var recovering = VolumeAdaptation.Adjust(band, band, new VolumeResponse(10, 10, 0, 0, 0.04m, 0.04m));
        Assert.Equal(new VolumeLandmarkValues(9, 16, 22), recovering);

        // Ravna nedelja ne pomera ništa: ćutanje nije svaka nedelja, nego ona koja bi nešto naučila pogrešno.
        var flat = VolumeAdaptation.Adjust(band, band, new VolumeResponse(10, 10, 0, 0, 0m, 0m));
        Assert.Equal(band, flat);
    }

    // ---- ocena umora --------------------------------------------------------------------------------------------

    private static decimal MaintenanceScore(decimal rirDeviation, decimal failureShare)
    {
        // Ravan blok, prva ili druga nedelja (ocena prve deluje na drugu, druge na treću; treće na četvrtu, koja je već
        // deload): čitanje snage nema prethodno (pa se ne potvrđuje), odgovora nema, a volumen je oko MEV-a, ispod praga
        // udela MRV-a (vidi TheVolumeSignal_IsZeroForEveryMaintenanceWeek).
        return FatigueEvaluator.Score(new WeeklyFatigue(
            rirDeviation,
            AchievableRirDeficit: 3m,
            failureShare,
            E1RmChangeShare: 0m,
            VolumeVsMrvShare: 0.4m,
            PreviousE1RmChangeShare: 0m,
            SubjectiveRise: 0m,
            PreviousSubjectiveRise: 0m));
    }

    /// <summary>
    /// Koliko često deload stiže u održavanju. Rezerva se meri prema dubini do koje serija sme da side ispod cilja (cilj
    /// 3, pa tri), signal volumena je nula, a u ravnom bloku nema potvrđene snage ni odgovora. Srednji nivo (prag 0.60)
    /// zato dostiže prag samo kad je pola serija do otkaza, a ostale su sve na RIR 0; napredni (0.50) uz pola do otkaza
    /// traži prosečno oko dve rezerve ispod cilja (0.25 / 0.35 pune rezerve od tri), a uz trećinu do otkaza pune tri.
    /// Znači: skoro nikad, osim u nedelji u kojoj je sve bilo na granici, što je baš nedelja kojoj
    /// deload treba. Izmereno nad pravom ocenom; volumen je ionako najmanji, pa deload tu daje malo.
    /// </summary>
    [Fact]
    public void InAFlatMaintenanceBlock_TheDeloadThreshold_IsReachedOnlyWhenEverySetIsGround()
    {
        var intermediate = ExperienceProgramming.DeloadThreshold(ExperienceLevel.Intermediate)!.Value;
        var advanced = ExperienceProgramming.DeloadThreshold(ExperienceLevel.Advanced)!.Value;

        // Prosečno jedna rezerva ispod cilja, i pola serija do otkaza: daleko ispod oba praga.
        Assert.True(MaintenanceScore(-1m, 0.5m) < advanced);
        // Dve ispod cilja: još ispod oba.
        Assert.True(MaintenanceScore(-2m, 0.5m) < advanced);
        // Sve dovršene serije na RIR 0 i pola do otkaza: tačno prag srednjeg nivoa.
        Assert.True(MaintenanceScore(-3m, 0.5m) >= intermediate);
        // Sa manje otkaza srednji nivo ne stiže, a napredni stiže tek sa oko trećinom do otkaza (0.3) uz pune tri rezerve.
        Assert.True(MaintenanceScore(-3m, 0.25m) < intermediate);
        Assert.True(MaintenanceScore(-3m, 0.25m) < advanced);
        Assert.True(MaintenanceScore(-3m, 0.3m) >= advanced);
        // Napredni uz pola do otkaza: prag je na prosečnih -2.14 (0.25 / 0.35 od tri), ne na -3.
        Assert.True(MaintenanceScore(-2.1m, 0.5m) < advanced);
        Assert.True(MaintenanceScore(-2.2m, 0.5m) >= advanced);

        // Isti nedeljni signali u hipertrofiji (cilj 1, pa dubina 1): pola serija do otkaza uz jednu rezervu ispod cilja
        // je već prag. Održavanje je tu tri puta manje osetljivo na rezervu, što je posledica toga da je cilj 3.
        var hypertrophy = FatigueEvaluator.Score(new WeeklyFatigue(-1m, 1m, 0.5m, 0m, 0.4m, 0m, 0m, 0m));
        Assert.True(hypertrophy >= intermediate);
    }

    /// <summary>
    /// Ocena u prethodnom testu uzima signal volumena kao nulu. To je pretpostavka o generisanoj nedelji, pa je ovde
    /// izmerena: nijedan mišić nijednog ugrađenog šablona na nijednom nivou ne prelazi 80% svog MRV-a u nedelji održavanja
    /// (prag na kome signal počinje da raste, <c>FatigueEvaluator</c>).
    /// </summary>
    [Fact]
    public void TheVolumeSignal_IsZeroForEveryMaintenanceWeek()
    {
        var highest = 0m;

        foreach (var week in TemplateWeekSimulation.EveryMaintenanceWeek())
        {
            foreach (var seed in ExerciseCatalog.VolumeLandmarks)
            {
                var mrv = ExperienceProgramming.ScaleLandmarks(
                    new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                    week.Level).Mrv;
                highest = Math.Max(highest, week.Weekly(seed.Muscle) / mrv);
            }
        }

        Assert.True(highest < 0.80m, $"highest share of MRV in a maintenance week: {highest:0.00}");
    }

    // ---- kalibracija --------------------------------------------------------------------------------------------

    /// <summary>
    /// Serija do otkaza bi bila guranje, pa je blok održavanja nema. Ugrađeni propis (6-10 uz tri u rezervi) bi je ionako
    /// preskočio, jer staje oko 13, iznad Epley granice (12); ali lični šablon zadržava opseg koji je korisnik uneo, i
    /// 5-8 uz tri u rezervi staje na 11. Pregled #129 je našao da je tu serija do otkaza ostajala, uprkos uputstvu.
    /// Zato odluka ne sledi iz brojeva nego iz cilja, a kontrola uz svaki opseg pokazuje da bi rastući blok sa istim
    /// opsegom tu seriju imao.
    /// </summary>
    [Theory]
    [InlineData(6, 10, 3)]
    [InlineData(5, 8, 3)]
    [InlineData(5, 5, 3)]
    public void ACalibrationSet_IsNeverPrescribedInAMaintenanceBlock_WhateverTheRange(int min, int max, int rir)
    {
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            Assert.False(CalibrationSet.AppliesTo(
                level,
                isLastTrainingWeek: true,
                order: 1,
                ExerciseType.Compound,
                suitsLowReps: true,
                max,
                rir,
                Goal.Maintenance));
        }

        // Kontrola: isti opseg u bloku koji gradi daje kalibraciju tamo gde se serija može pročitati (max + rir <= 12).
        Assert.Equal(
            max + rir <= TrainingConstants.EpleyRepCap,
            CalibrationSet.AppliesTo(
                ExperienceLevel.Intermediate,
                isLastTrainingWeek: true,
                order: 1,
                ExerciseType.Compound,
                suitsLowReps: true,
                max,
                rir,
                Goal.Hypertrophy));
    }

    /// <summary>
    /// Isto što i gore, ali na prelazu koji se ne vidi iz prostih brojeva: blok održavanja ne drži ništa što zavisi od
    /// toga da je rezerva tri.
    /// </summary>
    [Fact]
    public void TheMaintenanceRange_IsOneThatTheCalibrationCapWouldSkipAnyway()
    {
        var maintenance = GoalPrescriptions.ForGoal(Goal.Maintenance);

        Assert.True(CalibrationSet.ExpectedRepsAtFailure(maintenance.RepRangeMax, maintenance.TargetRir) > TrainingConstants.EpleyRepCap);
    }

    // ---- sastav dana i opšta pravila koja održavanje deli sa hipertrofijom -------------------------------------------

    /// <summary>
    /// Čarobnjak pokazuje dane hipertrofije i za održavanje (<c>TemplateService.DaysFor</c>), što je tačno samo dok
    /// budžet složenih vežbi za održavanje stoji kao za hipertrofiju. Ovo je to što ga drži, pa pregled dana u čarobnjaku
    /// i trening koji server sastavi ne mogu da se razilaze a da test ne pukne.
    /// </summary>
    [Fact]
    public void MaintenanceComposesEveryDay_AsHypertrophyDoes()
    {
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            Assert.Equal(
                ExperienceProgramming.MaxCompoundsPerSession(level, Goal.Hypertrophy),
                ExperienceProgramming.MaxCompoundsPerSession(level, Goal.Maintenance));

            foreach (var template in WorkoutTemplateCatalog.GetAll())
            {
                foreach (var day in template.Days)
                {
                    Assert.Equal(
                        SessionComposition.ForLevel(day.Exercises, ExerciseCatalog.IsCompound, level, Goal.Hypertrophy),
                        SessionComposition.ForLevel(day.Exercises, ExerciseCatalog.IsCompound, level, Goal.Maintenance));
                }
            }
        }
    }
}
