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

    [Theory]
    // Sklek: 0.64 od 80 kg je 51.2, što nije umnožak koraka od 1 kg. Ukupno 55.796 traži
    // 4.596 na pojasu, dakle 5; zaokruživanje ukupnog bi dalo 56 - 51.2 = 4.8, težinu koja
    // se ne može staviti ni na jedan pojas.
    [InlineData(55.796, 51.2, 1, 5)]
    [InlineData(60.4, 51.2, 1, 9)]
    // Iskorak: 0.85 od 80 kg je 68, uz korak od 2.5 kg takođe van mreže.
    [InlineData(75.6, 68, 2.5, 7.5)]
    [InlineData(70.1, 68, 2.5, 2.5)]
    public void AddedTarget_PutsTheResultOnTheStepGrid_EvenWhenTheBodyIsNot(
        decimal rawTotalKg,
        decimal portionKg,
        decimal stepKg,
        decimal expected)
    {
        // Ovo je jedino svojstvo zbog koga AddedTarget postoji: mreža koraka je na pojasu,
        // ne na telu. Sve dosadašnje provere su nosile deo tela od 80 kg uz korak od 2.5,
        // a 80 = 32 × 2.5 — za takav deo su dve različite implementacije identične. Ceo
        // paket (489 testova) je ostao zelen sa zaokruživanjem nad UKUPNIM opterećenjem,
        // koje vraća težinu van mreže koraka i onda je nosi dalje kao osnovu.
        var added = BodyweightLoad.AddedTarget(rawTotalKg, portionKg, stepKg);

        Assert.Equal(expected, added);
        Assert.Equal(0m, added % stepKg);
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
        // cilju 1. Korak je 1 kg, koliko EquipmentWeightStep daje opremi „Bodyweight" —
        // isti broj koji DbSeeder upisuje vežbi. Test je prvo koristio 2.5 kg i time
        // pokazivao napredak koji zgib u aplikaciji nikada ne bi dobio.
        var result = _engine.ComputeNext(
            usedWeightKg: 10m,
            workingSets: [new WorkingSet(12, 3), new WorkingSet(12, 3), new WorkingSet(12, 3)],
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: EquipmentWeightStep.ForEquipment(BodyweightLoad.BodyweightEquipment),
            bodyweightLoadKg: 80m);

        // Ukupno 90 -> 90 × 1.06 + 1 = 96.4; na pojas ide 16.
        Assert.Equal(16m, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
        Assert.False(result.LoadFloorReached);
    }

    [Fact]
    public void ComputeNext_OnTheOldRule_WouldHaveProposedFourKilogramsLess()
    {
        // Merenje koje stoji u docs/features/bodyweight-load.md: bez dela telesne mase se
        // isti trening skalirao samo nad dodatim kilogramima, pa je korekcija od 6%
        // vredela 0.6 kg umesto 5.4.
        var sets = new[] { new WorkingSet(12, 3), new WorkingSet(12, 3), new WorkingSet(12, 3) };
        var step = EquipmentWeightStep.ForEquipment(BodyweightLoad.BodyweightEquipment);

        var withoutBody = _engine.ComputeNext(10m, sets, 1, 8, 12, step);
        var withBody = _engine.ComputeNext(10m, sets, 1, 8, 12, step, 80m);

        Assert.Equal(12m, withoutBody.NextWeightKg);
        Assert.Equal(16m, withBody.NextWeightKg);
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
    public void ComputeNext_WithoutABodyPortion_StillFollowsTheRuleThatWasThereBefore()
    {
        // Ovaj test je poredio poziv sa samim sobom: bodyweightLoadKg ima podrazumevanu
        // vrednost 0, pa su dva izraza bila ISTI poziv iste čiste metode i tvrdnja nije
        // mogla da padne ni za jednu implementaciju. Sada se poredi sa brojem koji pravilo
        // daje: prosečan RIR 2 na cilju 1 je +3%, 105 × 1.03 = 108.15, zaokruženo 107.5.
        var sets = new[] { new WorkingSet(10, 2), new WorkingSet(10, 2), new WorkingSet(9, 2) };

        var result = _engine.ComputeNext(105m, sets, 1, 8, 12, 2.5m);

        Assert.Equal(107.5m, result.NextWeightKg);
        Assert.True(result.WeightIncreased);
        Assert.False(result.LoadFloorReached);
    }

    [Fact]
    public void ComputeNext_ProposesNoStep_WhenAnExternalExerciseWasLoggedAtZero()
    {
        // Ponašanje koje je ova grana promenila za vežbe BEZ telesne mase: pre nje je
        // 0 kg na vrhu opsega davalo 0 × 1 + 2.5 = 2.5 kg, dakle „stavi tanjir" na seriju
        // koju niko nije opteretio. Ostatak paketa to ne vidi: mreža težina u
        // ProgressionPropertyTests počinje od jednog koraka, nikada od nule.
        var result = _engine.ComputeNext(
            usedWeightKg: 0m,
            workingSets: [new WorkingSet(12, 1), new WorkingSet(12, 1), new WorkingSet(12, 1)],
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m);

        Assert.Equal(0m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
        Assert.True(result.LoadFloorReached);
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
    public void NextWeekLoad_DerivesANewPrescriptionFromTheTotalMaximum()
    {
        // Naredna nedelja traži drugačiji propis, a maksimum je poznat: 109.33 kg je
        // procena iz zgiba bez pojasa (telo od 80 kg, deset ponavljanja sa RIR 1). Radno
        // opterećenje za 8 sa RIR 1 je 109.33 / 1.3 = 84.10 ukupno, pa na pojas ide 4.
        // Bez dela tela bi ovde stajalo 84 kg DODATIH — 164 kg ukupno za vežbača od 80.
        var next = NextWeekLoad.For(
            referenceWeightKg: 0m,
            progressionWeightKg: 0m,
            current: new LoadPrescription(11, 12, 1),
            next: new LoadPrescription(8, 12, 1),
            nextIsDeload: false,
            oneRepMaxKg: 109.33m,
            weightStepKg: 1m,
            bodyweightLoadKg: 80m);

        Assert.Equal(4m, next);
    }

    [Fact]
    public void NextWeekLoad_DeloadsFromTheTotalMaximum_WhenNothingWasLogged()
    {
        // Preskočena vežba pred deload: opterećenje se izvodi iz maksimuma i rasterećuje.
        // 109.33 / 1.3 = 84.10 ukupno, 90% je 75.69 — ispod tela od 80, pa deload nedelja
        // propisuje sopstvenu masu.
        var next = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 1),
            nextIsDeload: true,
            oneRepMaxKg: 109.33m,
            weightStepKg: 1m,
            bodyweightLoadKg: 80m);

        Assert.Equal(0m, next);
    }

    [Fact]
    public void NextWeekLoad_CarriesTheMaximumIntoAnUntrainedWeek_OnTheAddedScale()
    {
        // Isti propis, ali o vežbi nema ni serija ni progresije: opterećenje se izvodi iz
        // maksimuma. 150 / 1.3 = 115.38 ukupno -> 35 na pojasu (telo 80, korak 1).
        var next = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 1),
            nextIsDeload: false,
            oneRepMaxKg: 150m,
            weightStepKg: 1m,
            bodyweightLoadKg: 80m);

        Assert.Equal(35m, next);
    }

    [Fact]
    public void UndoDeload_KeepsTheFloorInsteadOfInventingLoad()
    {
        // Zgib rasterećen na sopstvenu masu: deload cilj je 0 dodatnih. Deljenje sa 0.9
        // nad ukupnim je odatle „vraćalo" 80 / 0.9 - 80 = 8.888…, pa je nedelja posle
        // deload-a propisivala „TM + 8 kg" vežbaču koji nikada nije dodao ni kilogram.
        // Nula se ne može obrnuti — AddedTarget je tu odsekao — pa se i vraća kao nula.
        Assert.Equal(0m, NextWeekLoad.UndoDeload(0m, 1m, 80m));

        // Bez dela tela je to isto ponašanje koje je bilo i pre ove grane.
        Assert.Equal(0m, NextWeekLoad.UndoDeload(0m, 2.5m));
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
