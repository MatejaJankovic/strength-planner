using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Izolacija u opsegu 10-20: kako ga periodizacija pomera, šta se iz takvih serija čita kao
/// promena snage, i da ništa što radi sa opsegom ne puca iznad Epley granice od 12.
/// </summary>
public class IsolationRepRangeTests
{
    private static readonly Guid LateralRaise = new("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Bench = new("00000000-0000-0000-0000-0000000000c2");

    private static readonly GoalPrescription Isolation =
        GoalPrescriptions.ForExercise(Goal.Hypertrophy, ExerciseType.Isolation, suitsLowReps: true);

    [Theory]
    [InlineData(12, 12)]
    [InlineData(6, 12)]
    [InlineData(13, 20)]
    [InlineData(20, 20)]
    public void TheCap_FollowsTheRangeThePlanCarries(int baseRepRangeMax, int expectedCap)
    {
        // Izolacija iz bloka napravljenog pre ove izmene nosi 8-12 i čita granicu 12, kao i
        // ranije: blok se čita onako kako je napisan.
        Assert.Equal(expectedCap, Periodization.MaxRepsFor(baseRepRangeMax));
    }

    public static TheoryData<PeriodizationModel> Models()
    {
        var data = new TheoryData<PeriodizationModel>();
        foreach (var model in Enum.GetValues<PeriodizationModel>())
        {
            data.Add(model);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void AnIsolationWeek_StaysInsideItsRange_AndKeepsItsWidth(PeriodizationModel model)
    {
        foreach (var week in Periodization.ForBlock(model, Isolation.RepRangeMin, Isolation.RepRangeMax, Isolation.TargetRir, 4))
        {
            Assert.InRange(week.RepRangeMax, Periodization.MinReps, Periodization.IsolationMaxReps);
            Assert.Equal(Isolation.RepRangeMax - Isolation.RepRangeMin, week.RepRangeMax - week.RepRangeMin);
        }
    }

    /// <summary>
    /// Pravilo iz runde 10 - svaka trenažna nedelja ima svoj propis - važi i za izolaciju.
    /// </summary>
    [Theory]
    [MemberData(nameof(Models))]
    public void EveryTrainingWeekOfAnIsolation_HasItsOwnPrescription(PeriodizationModel model)
    {
        if (model == PeriodizationModel.Flat)
        {
            return;
        }

        var weeks = Periodization
            .ForBlock(model, Isolation.RepRangeMin, Isolation.RepRangeMax, Isolation.TargetRir, 4)
            .Where(week => !week.IsDeload)
            .Select(week => (week.Sets, week.RepRangeMin, week.RepRangeMax, week.TargetRir))
            .ToList();

        Assert.Equal(weeks.Count, weeks.Distinct().Count());
    }

    /// <summary>
    /// Na vrhu od 20 izolacija se ponaša kao hipertrofija na 12: pomeraj naviše koji granica
    /// proguta vraća se kao serija, isto kao kod 8-12 pre izmene.
    /// </summary>
    [Fact]
    public void AnIsolationAtItsCap_GetsTheSwallowedShiftAsASet_LikeEightToTwelveDid()
    {
        var isolation = Periodization.ForBlock(PeriodizationModel.Inverse, 10, 20, 1, 4).Select(week => week.Sets);
        var eightToTwelve = Periodization.ForBlock(PeriodizationModel.Inverse, 8, 12, 1, 4).Select(week => week.Sets);

        Assert.Equal(eightToTwelve, isolation);
    }

    /// <summary>
    /// Korak od 2 kg na bočnom podizanju od 8 kg je 25%. Opseg 8-12 uz RIR 1 upija oko 13%,
    /// pa je korak tražio produžen cilj od 17 ponavljanja; 10-20 upija oko 27%, pa korak staje
    /// u sam opseg.
    /// </summary>
    [Fact]
    public void ALightDumbbellStep_FitsTheIsolationRange()
    {
        Assert.False(StepAbsorption.FitsAtTarget(8m, 2m, 8, 12, 1));
        Assert.Equal(17, StepAbsorption.RepsToEarnStep(8m, 2m, 8, 12, 1));

        Assert.True(StepAbsorption.FitsAtTarget(8m, 2m, Isolation.RepRangeMin, Isolation.RepRangeMax, Isolation.TargetRir));
        Assert.Equal(20, StepAbsorption.RepsToEarnStep(8m, 2m, Isolation.RepRangeMin, Isolation.RepRangeMax, Isolation.TargetRir));
        // Bučica od 5 kg: korak od 2 kg je 40%, pa ni 10-20 ne upija. Uputstvo navodi ovaj broj.
        Assert.Equal(25, StepAbsorption.RepsToEarnStep(5m, 2m, Isolation.RepRangeMin, Isolation.RepRangeMax, Isolation.TargetRir));
    }

    /// <summary>
    /// Serije od 13 do 20 ne daju procenu maksimuma, pa bi ramena i listovi - koje treniraju
    /// samo izolacije - prestali da uče granice volumena. Na istom teretu se porede efektivna
    /// ponavljanja: 10 kg, 13 + 1 pa 15 + 1, po Epley-u je (30 + 16) / (30 + 14) - 1.
    /// </summary>
    [Fact]
    public void AboveTheCap_TheSameLoadIsComparedByReps()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(LateralRaise, 15, 1, 10m)],
            [new StrengthSample(LateralRaise, 13, 1, 10m)]);

        Assert.NotNull(change);
        Assert.Equal(46m / 44m - 1m, change!.Value, precision: 6);
    }

    [Fact]
    public void AboveTheCap_FewerRepsAtTheSameLoad_ReadAsADecline()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(LateralRaise, 14, 1, 10m)],
            [new StrengthSample(LateralRaise, 17, 1, 10m)]);

        Assert.True(change < 0m);
    }

    /// <summary>
    /// Nedelja posle koraka: teret je porastao, a ponavljanja pala. Na istom teretu nema
    /// para, pa nema ni čitanja - tišina, ne pad.
    /// </summary>
    [Fact]
    public void AboveTheCap_ADifferentLoad_GivesNothing()
    {
        Assert.Null(StrengthChange.ChangeShare(
            [new StrengthSample(LateralRaise, 11, 1, 12m)],
            [new StrengthSample(LateralRaise, 20, 1, 10m)]));
    }

    /// <summary>
    /// Dve serije do 12 ponavljanja na istom teretu su posao procene, i gde je ona odbila par
    /// (efektivna ponavljanja predaleko), ovo pravilo ga ne sme upariti iza njenih leđa. Za
    /// složene vežbe se time ništa ne menja.
    /// </summary>
    [Fact]
    public void AtOrBelowTheCap_TheEstimateRuleIsUnchanged()
    {
        Assert.Null(StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 8, 1, 100m)],
            [new StrengthSample(Bench, 12, 1, 100m)]));
    }

    [Fact]
    public void AboveTheCap_ASetFarFromFailure_IsNoEvidence()
    {
        Assert.Null(StrengthChange.ChangeShare(
            [new StrengthSample(LateralRaise, 18, 5, 10m)],
            [new StrengthSample(LateralRaise, 14, 1, 10m)]));
    }

    /// <summary>
    /// Kad vežba ima par procena, čita se on; poređenje na istom teretu popunjava samo
    /// prazninu. Ovde procena kaže rast (100 x 8 + 1 pa 105 x 8 + 1), a isti teret bi rekao
    /// pad - da je čitan, prosek ne bi bio ovaj.
    /// </summary>
    [Fact]
    public void WhenAnEstimatePairExists_ItIsTheOneRead()
    {
        var change = StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 8, 1, 105m), new StrengthSample(Bench, 13, 1, 80m)],
            [new StrengthSample(Bench, 8, 1, 100m), new StrengthSample(Bench, 16, 1, 80m)]);

        Assert.Equal(0.05m, change!.Value, precision: 6);
    }

    /// <summary>
    /// Lični šablon sme izolaciji da propiše 15-20. Nedelja koja iz takvog opsega prelazi
    /// u drugi je tražila procenu maksimuma sa 15 ponavljanja, a procena iznad 12 baca
    /// izuzetak. Prenos težine je odnos na krivoj, pa granicu ne treba ni da čita.
    /// </summary>
    [Fact]
    public void ALoadMovesBetweenTwoHighRepWeeks_WithoutAnEstimate()
    {
        var load = NextWeekLoad.For(
            referenceWeightKg: 10m,
            progressionWeightKg: null,
            current: new LoadPrescription(15, 20, 1),
            next: new LoadPrescription(13, 18, 1),
            nextIsDeload: false,
            oneRepMaxKg: null,
            weightStepKg: 1m);

        // 10 x (30 + 16) / (30 + 14) = 10.45, na koraku od 1 kg: 10.
        Assert.Equal(10m, load);
    }

    /// <summary>
    /// Nalaz review-a PR #91. Linearan blok, bočno podizanje: nedelja 2 odrađena 10 kg x 20
    /// uz RIR 2, progresija traži 12 kg - a nedelja 3 ima drugi propis, pa se težina izvodila
    /// iz procene u prozoru. Serije preko 12 procenu ne upisuju, pa je u prozoru stajalo
    /// 11.2 kg iz ranije serije na 8 kg, i nedelja 3 je dobijala 8 kg. U opsegu preko Epley
    /// granice sada govori sama težina treninga.
    /// </summary>
    [Fact]
    public void AnIsolationRange_ScalesTheSessionsOwnLoad_NotAnOlderEstimate()
    {
        var load = NextWeekLoad.For(
            referenceWeightKg: 10m,
            progressionWeightKg: 12m,
            current: new LoadPrescription(10, 20, 2),
            next: new LoadPrescription(10, 20, 1),
            nextIsDeload: false,
            oneRepMaxKg: 11.2m,
            weightStepKg: 2m);

        // 12 x (30 + 12) / (30 + 11) = 12.29, na koraku od 2 kg: 12.
        Assert.Equal(12m, load);
    }

    /// <summary>
    /// Kad za izolaciju nije poznata ni odrađena ni planirana težina, maksimum ostaje
    /// rezerva: bolje predlog iz starije procene nego nikakav.
    /// </summary>
    [Fact]
    public void AnIsolationRange_FallsBackToTheMaximum_WhenNoLoadIsKnown()
    {
        var load = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: new LoadPrescription(10, 20, 2),
            next: new LoadPrescription(10, 20, 1),
            nextIsDeload: false,
            oneRepMaxKg: 14m,
            weightStepKg: 2m);

        // 14 / (1 + 11 / 30) = 10.24, na koraku od 2 kg: 10.
        Assert.Equal(10m, load);
    }

    [Fact]
    public void ACompoundRange_StillReadsTheMaximum_WhenThePrescriptionChanges()
    {
        var load = NextWeekLoad.For(
            referenceWeightKg: 100m,
            progressionWeightKg: 102.5m,
            current: new LoadPrescription(8, 12, 2),
            next: new LoadPrescription(6, 10, 1),
            nextIsDeload: false,
            oneRepMaxKg: 140m,
            weightStepKg: 2.5m);

        // 140 / (1 + 7 / 30) = 113.5, na koraku od 2.5 kg: 112.5 - iz maksimuma, ne iz 102.5.
        Assert.Equal(112.5m, load);
    }

    /// <summary>
    /// Kad obe nedelje imaju procenu, odlučuje ona - i kad kaže da par nije uporediv.
    /// Serije preko 12 na istoj težini tada ne smeju da se upare iza njenih leđa: bench
    /// 100 x 6 pa 100 x 9 (neuporedivo) uz 80 x 13 pa 80 x 16 bi inače čitao -6.4%.
    /// </summary>
    [Fact]
    public void WhenBothWeeksHaveEstimates_TheSameLoadRuleStaysOut()
    {
        Assert.Null(StrengthChange.ChangeShare(
            [new StrengthSample(Bench, 6, 1, 100m), new StrengthSample(Bench, 13, 1, 80m)],
            [new StrengthSample(Bench, 9, 1, 100m), new StrengthSample(Bench, 16, 1, 80m)]));
    }

    [Fact]
    public void TheImpliedMaximum_IsTheEstimate_WhereAnEstimateExists()
    {
        var calculator = new E1RmCalculator();

        Assert.Equal(calculator.EstimateOneRepMax(100m, 8, 2), calculator.ImpliedOneRepMax(100m, 8, 2));
        Assert.Throws<ArgumentException>(() => calculator.EstimateOneRepMax(10m, 15, 1));
        Assert.Equal(10m * (1 + 16m / 30m), calculator.ImpliedOneRepMax(10m, 15, 1));
    }
}
