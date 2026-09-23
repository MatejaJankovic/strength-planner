using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Vežba koju opterećuje sopstveno telo.
///
/// Zgib je upisivan kao 0 kg, pa je sve niže u lancu čitalo „nema opterećenja": procena
/// maksimuma je bila 0 i ulazila u rekorde kao takva, tonaža nije brojala ništa, a
/// progresija je predlagala „korak više" na seriji koja nikada nije bila opterećena. Telo je
/// opterećenje, i profil već nosi njegovu masu.
/// </summary>
public class BodyweightLoadTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    [InlineData(80, 1.00, 80)]
    [InlineData(80, 0.64, 51.2)]
    [InlineData(80, 0.85, 68)]
    [InlineData(68.5, 0.64, 43.84)]
    // Profil bez unete mase: vežba se ponaša kao pre, računa se samo dodato.
    [InlineData(0, 1.00, 0)]
    // Vežba bez udela (klupa, plank): telo se ne računa.
    [InlineData(80, 0, 0)]
    // Udeo veći od celog tela nema smisla i seče se na telo.
    [InlineData(80, 1.50, 80)]
    public void PortionKg_IsTheShareOfBodyMassTheMovementLifts(
        decimal bodyweightKg,
        decimal share,
        decimal expected)
    {
        Assert.Equal(expected, BodyweightLoad.PortionKg(bodyweightKg, share));
    }

    [Fact]
    public void PortionKg_RoundsToTheTwoDecimalsTheColumnStores()
    {
        // numeric(6,2): vrednost koja se ne zaokruži ovde, zaokružila bi se pri upisu, pa
        // bi izračunato i snimljeno opterećenje bila dva različita broja.
        Assert.Equal(56.33m, BodyweightLoad.PortionKg(88.01m, 0.64m));
    }

    [Theory]
    // Ukupno 97.9 na telo od 80: na pojas ide 17.5 (korak 2.5).
    [InlineData(97.9, 80, 2.5, 17.5)]
    // Ukupno tačno telo: ništa se ne dodaje.
    [InlineData(80, 80, 2.5, 0)]
    // Traženo ukupno je ISPOD tela: nema šta da se skine, pa 0 — a ne negativan teg.
    [InlineData(72, 80, 2.5, 0)]
    // Bez tela je to obično zaokruživanje na korak.
    [InlineData(101.2, 0, 2.5, 100)]
    public void AddedTarget_RoundsInAddedSpaceAndNeverGoesNegative(
        decimal rawTotalKg,
        decimal portionKg,
        decimal stepKg,
        decimal expected)
    {
        Assert.Equal(expected, BodyweightLoad.AddedTarget(rawTotalKg, portionKg, stepKg));
    }

    [Fact]
    public void IsAtBodyweightFloor_OnlyWhenTheWantedTotalIsLighterThanTheBody()
    {
        Assert.True(BodyweightLoad.IsAtBodyweightFloor(72m, 80m));
        Assert.False(BodyweightLoad.IsAtBodyweightFloor(80m, 80m));
        Assert.False(BodyweightLoad.IsAtBodyweightFloor(90m, 80m));
        // Vežba bez tela nikada nije na tom podu: tamo teg ima kuda da se skine.
        Assert.False(BodyweightLoad.IsAtBodyweightFloor(5m, 0m));
    }

    [Fact]
    public void ComputeNext_ScalesTheWholeLoadAndReturnsWhatGoesOnTheBelt()
    {
        // Zgib sa +10 kg na vežbaču od 80 kg, tri serije po 12 (opseg 8-12) sa RIR 3 na
        // cilju 1. Bez tela se korekcija od 6% primenjivala na DODATIH 10 kg, pa je
        // predlog rastao za 0.6 kg umesto za 5.4 — zgib je „napredovao" polovinom koraka.
        var result = _engine.ComputeNext(
            usedWeightKg: 10m,
            workingSets: [new WorkingSet(12, 3), new WorkingSet(12, 3), new WorkingSet(12, 3)],
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m,
            bodyweightLoadKg: 80m);

        // Ukupno 90 -> 90 * 1.06 + 2.5 = 97.9; na pojas ide 17.5.
        Assert.Equal(17.5m, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
        Assert.False(result.LoadFloorReached);
    }

    [Fact]
    public void ComputeNext_StopsAtBodyMassAndSaysSo_WhenTheRuleWantsLess()
    {
        // Pet zgibova do otkaza u opsegu 8-12: kapacitet je tri ponavljanja ispod dna, pa
        // korekcija pada na -10%. Ukupno 72 kg je ispod tela od 80 — nema šta da se skine.
        var result = _engine.ComputeNext(
            usedWeightKg: 0m,
            workingSets:
            [
                new WorkingSet(5, 0, IsFailure: true),
                new WorkingSet(5, 0, IsFailure: true),
                new WorkingSet(5, 0, IsFailure: true)
            ],
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m,
            bodyweightLoadKg: 80m);

        Assert.Equal(0m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
        // Poruka za korisnika: napredak ide kroz ponavljanja, ne kroz kilograme.
        Assert.True(result.LoadFloorReached);
    }

    [Fact]
    public void ComputeNext_ProposesNothingAndNoStep_WhenThereIsNoLoadAtAll()
    {
        // Plank upisan sa 0 kg i bez udela telesne mase. Do sada je vrh opsega ovde
        // predlagao korak više na vežbi koja se ne opterećuje tegovima.
        var result = _engine.ComputeNext(
            usedWeightKg: 0m,
            workingSets: [new WorkingSet(30, 0, IsFailure: true)],
            targetRir: 0,
            repRangeMin: 20,
            repRangeMax: 30,
            weightStepKg: 2.5m);

        Assert.Equal(0m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
        Assert.True(result.LoadFloorReached);
    }

    [Fact]
    public void ComputeNext_WithoutABodyPortion_IsExactlyWhatItWasBefore()
    {
        var sets = new[] { new WorkingSet(10, 2), new WorkingSet(10, 2), new WorkingSet(9, 1) };

        var withoutParameter = _engine.ComputeNext(100m, sets, 1, 8, 12, 2.5m);
        var withZero = _engine.ComputeNext(100m, sets, 1, 8, 12, 2.5m, 0m);

        Assert.Equal(withoutParameter, withZero);
    }

    [Fact]
    public void WorkingLoad_ComparesSetsByTheirTotalLoad()
    {
        // Dve serije zgiba: jedna sa 5 kg, jedna bez pojasa. Razlika je sitna naspram tela
        // koje obe nose, ali teža je ona sa pojasom.
        var load = WorkingLoad.Select([
            new LoggedSet(0m, 10, 1, BodyweightLoadKg: 80m),
            new LoggedSet(5m, 8, 1, BodyweightLoadKg: 80m)
        ]);

        Assert.NotNull(load);
        Assert.Equal(5m, load!.ReferenceWeightKg);
        Assert.Equal(80m, load.ReferenceBodyweightLoadKg);
        Assert.Equal(85m, load.ReferenceTotalLoadKg);
        Assert.Equal(1, load.ExcludedLighterSets);
    }

    [Fact]
    public void BestEstimate_ReadsTheBodyAsLoad()
    {
        var calculator = new E1RmCalculator();

        // Deset zgibova sa RIR 1 na telu od 80 kg: 80 * (1 + 11/30) = 109.33.
        var estimate = calculator.BestEstimate([new LoggedSet(0m, 10, 1, BodyweightLoadKg: 80m)]);

        Assert.NotNull(estimate);
        Assert.Equal(109.33m, Math.Round(estimate!.Value, 2));
    }

    [Fact]
    public void BestEstimate_StaysNull_WhenNothingCarriedAnyLoad()
    {
        var calculator = new E1RmCalculator();

        // Plank: ni tela, ni tegova. Do sada je 0 ulazio u rekorde kao maksimum.
        Assert.Null(calculator.BestEstimate([new LoggedSet(0m, 10, 0)]));
    }

    [Fact]
    public void NextWeekLoad_DeloadsToNinetyPercentOfTheTotal()
    {
        // Zgib sa +10 na telu od 80: ukupno 90, deload 81, na pojas ide 0 — telo samo je
        // već 80. Bez tela je 90% od 10 bilo 9 kg, dakle rasterećenje od jednog kilograma.
        var next = NextWeekLoad.For(
            referenceWeightKg: 10m,
            progressionWeightKg: 12.5m,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 1),
            nextIsDeload: true,
            oneRepMaxKg: null,
            weightStepKg: 2.5m,
            bodyweightLoadKg: 80m);

        Assert.Equal(0m, next);
    }

    [Fact]
    public void UndoDeload_RestoresTheLoadOnTheTotalScale()
    {
        // Deload od 80 + 5 = 85 ukupno; podeljeno sa 0.9 daje 94.4, pa na pojas 12.5
        // (zaokruženo naniže). Bez tela bi 5 / 0.9 = 5.6 vratilo 5 kg, kao da deload-a
        // nije ni bilo.
        Assert.Equal(12.5m, NextWeekLoad.UndoDeload(5m, 2.5m, 80m));

        // Bez dela tela pravilo je ono što je bilo: 90 / 0.9 = 100.
        Assert.Equal(100m, NextWeekLoad.UndoDeload(90m, 2.5m));
    }

    [Fact]
    public void Catalog_OnlyGivesABodyShareToExercisesLoadedByTheBody()
    {
        foreach (var exercise in ExerciseCatalog.Exercises)
        {
            Assert.True(
                BodyweightLoad.IsValidShare(exercise.BodyweightShare),
                $"{exercise.Name}: udeo {exercise.BodyweightShare} je van opsega [0, 1].");

            if (exercise.BodyweightShare > 0)
            {
                Assert.True(
                    BodyweightLoad.IsBodyweightExercise(exercise.Equipment),
                    $"{exercise.Name}: nosi udeo telesne mase, a oprema je {exercise.Equipment}.");
            }
        }
    }

    [Fact]
    public void Catalog_GivesEveryRepBasedBodyweightExerciseAShare()
    {
        // Vežba sa telesnom masom bez udela je tačno greška zbog koje ova runda postoji:
        // 0 znači „nema opterećenja". Plank je izuzet i imenovan — izdržaj nema
        // ponavljanje čije bi se opterećenje procenjivalo.
        var missing = ExerciseCatalog.Exercises
            .Where(exercise => BodyweightLoad.IsBodyweightExercise(exercise.Equipment)
                               && exercise.BodyweightShare == 0
                               && exercise.Name != "Plank")
            .Select(exercise => exercise.Name)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void BuiltInTemplates_NoLongerUseTheExerciseThatCannotBeLoaded()
    {
        // Plank ostaje u katalogu (stari planovi, lični šabloni, strani ključevi), ali ga
        // ugrađeni šabloni više ne propisuju: petnaest mesta je zamenjeno Machine
        // Crunch-om, koji se opterećuje i zato može da napreduje.
        var usesPlank = WorkoutTemplateCatalog.GetAll()
            .SelectMany(template => template.Days)
            .SelectMany(day => day.Exercises)
            .Any(name => name == "Plank");

        Assert.False(usesPlank);
    }
}
