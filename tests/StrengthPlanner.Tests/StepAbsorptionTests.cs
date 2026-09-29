using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Korak koji opseg ne može da upije, korekcija koju korak ne može da izrazi, i deload koji
/// se zaokruživao nazad na punu težinu. Sva tri su isti koren: korak tega veliki u odnosu na
/// težinu, što je kod bučica i malih mašina uobičajeno.
///
/// Mreža težina u <see cref="ProgressionPropertyTests"/> je te težine pokrivala i ranije, ali
/// je tvrdila staro pravilo - korak na vrhu opsega na svakoj težini, pa i 8 -> 10 kg - pa je
/// grešku zaključavala umesto da je hvata.
/// </summary>
public class StepAbsorptionTests
{
    private static readonly decimal[] Steps = [0.5m, 1m, 2m, 2.5m, 5m, 10m];

    private static readonly (int Min, int Max)[] Ranges = [(3, 6), (8, 12), (11, 12), (3, 4), (5, 5), (6, 9)];

    private readonly ProgressionEngine _engine = new();

    // --- cilj ponavljanja koji ekran prikazuje ---

    [Theory]
    // Šipka na stvarnim težinama: korak uvek staje, cilj ostaje vrh opsega.
    [InlineData(100, 2.5, 8, 12, 1, 12)]
    [InlineData(60, 2.5, 3, 6, 2, 6)]
    // Bučica 20 -> 22 kg je 10% i staje; 8 -> 10 kg je 25% i ne staje.
    [InlineData(20, 2, 8, 12, 1, 12)]
    [InlineData(8, 2, 8, 12, 1, 17)]
    // Mašina 20 -> 25 kg (25%), sajla 12.5 -> 15 kg (20%), šipka od 30 kg u 11-12 @RIR1.
    [InlineData(20, 5, 8, 12, 1, 17)]
    [InlineData(12.5, 2.5, 8, 12, 1, 15)]
    [InlineData(30, 2.5, 11, 12, 1, 14)]
    // Uzak ili fiksan propis se ne produžava: 5 x 5 je program, a ne opseg do šest.
    [InlineData(100, 2.5, 11, 12, 2, 12)]
    [InlineData(30, 2.5, 11, 12, 2, 12)]
    [InlineData(100, 2.5, 5, 5, 2, 5)]
    [InlineData(40, 2.5, 5, 5, 2, 5)]
    [InlineData(60, 2.5, 5, 5, 1, 5)]
    public void RepsToEarnStep_IsTheTopOfTheRange_UnlessTheStepDoesNotFitAWideRange(
        double loadKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int expected)
    {
        var reps = StepAbsorption.RepsToEarnStep((decimal)loadKg, (decimal)stepKg, repRangeMin, repRangeMax, targetRir);

        Assert.Equal(expected, reps);
    }

    [Fact]
    public void RepsToEarnStep_IsTheSmallestRepCountThatKeepsTheFloorReachable()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var load in Enumerable.Range(1, 120).Select(k => k * step))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        var reps = StepAbsorption.RepsToEarnStep(load, step, min, max, targetRir);
                        var described = $"{load}/{step} {min}-{max}@{targetRir}: {reps}";

                        if (StepAbsorption.IsNarrow(min, max, targetRir))
                        {
                            if (reps != max)
                            {
                                failures.Add($"{described} stretches a narrow prescription");
                            }

                            continue;
                        }

                        if (reps < max)
                        {
                            failures.Add($"{described} below the top");
                        }

                        if (!FloorReachable(reps, targetRir, load, step, min))
                        {
                            failures.Add($"{described} leaves the floor beyond failure");
                        }

                        if (reps > max && FloorReachable(reps - 1, targetRir, load, step, min))
                        {
                            failures.Add($"{described} is not the smallest");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    // --- progresija na lakim težinama ---

    [Fact]
    public void ALightDumbbell_WaitsForTheCapacityThatPaysForTheStep_InsteadOfJumping25Percent()
    {
        // Izmereno na kodu pre ove izmene: 8 kg, 3 x 12 @RIR1 -> 10 kg, gde Epley ostavlja
        // oko 3.4 ponavljanja uz RIR 1 u opsegu koji počinje od 8.
        Assert.Equal(8m, Next(8m, 12, 1, 8, 12, 1, 2m));
        Assert.Equal(8m, Next(8m, 16, 1, 8, 12, 1, 2m));
        Assert.Equal(10m, Next(8m, 17, 1, 8, 12, 1, 2m));
    }

    [Fact]
    public void TheCapacityCounts_NotTheRepsAlone()
    {
        // Kapacitet je ponavljanja plus rezerva koja je zaista ostala. 17 do otkaza je
        // kapacitet 17, a korak traži 17.5: težina čeka. 15 uz RIR 3 je 18: korak.
        Assert.Equal(8m, Next(8m, 17, 0, 8, 12, 1, 2m, isFailure: true));
        Assert.Equal(10m, Next(8m, 15, 3, 8, 12, 1, 2m));
    }

    [Fact]
    public void ALightLoad_GetsExactlyOneStep_NotAStepAndACorrection()
    {
        // 12 kg, 3 x 12 @RIR4: kapacitet upija jedan korak (14 kg), a korekcija +9% povrh
        // njega bi zaokruživanjem dala 16 kg, gde po Epley-u ostaje 4.5 ponavljanja.
        Assert.Equal(14m, Next(12m, 12, 4, 8, 12, 1, 2m));
    }

    [Fact]
    public void AMachineStepThatDoesNotFit_HoldsTheLoad()
    {
        Assert.Equal(20m, Next(20m, 12, 1, 8, 12, 1, 5m));
    }

    [Fact]
    public void WhileTheLoadWaits_ReserveAboveTarget_AppliesAsItWouldInsideTheRange()
    {
        // 8 kg, 3 x 12 @RIR3: korekcija +6% je 8.48, zaokruženo 8 - čeka.
        Assert.Equal(8m, Next(8m, 12, 3, 8, 12, 1, 2m));

        // 28 kg u 11-12 @RIR1, korak 2 (7%, ne staje): dve lake serije i jedna na cilju.
        // Kapacitet serije na cilju ne upija korak, pa ide sama korekcija (+8% -> 30) -
        // isto koliko i ista vežba jedno ponavljanje ispod vrha.
        var atTop = new List<WorkingSet> { new(12, 5), new(12, 5), new(12, 1) };
        var belowTop = new List<WorkingSet> { new(11, 5), new(12, 5), new(12, 1) };

        Assert.Equal(30m, _engine.ComputeNext(28m, atTop, 1, 11, 12, 2m).NextWeightKg);
        Assert.Equal(30m, _engine.ComputeNext(28m, belowTop, 1, 11, 12, 2m).NextWeightKg);
    }

    [Fact]
    public void TheTopOfTheRange_NeverGivesLessThanTheSameSetOneRepBelowIt()
    {
        // 40 kg u 11-12 @RIR1 (korak od 6% ne staje u opseg od jednog ponavljanja). 11 uz
        // RIR 5 je ispod vrha, i korekcija (+10%) daje 45. 12 uz RIR 5 upija korak, a
        // davalo bi samo jedan korak (42.5) da korak nije dobio korekciju kao donju granicu.
        // Uhvaćeno testom monotonosti (prvi put na fiksnom 5-5, koji je posle vraćen na
        // staro pravilo).
        Assert.Equal(45m, Next(40m, 11, 5, 11, 12, 1, 2.5m));
        Assert.Equal(45m, Next(40m, 12, 5, 11, 12, 1, 2.5m));
    }

    [Fact]
    public void AnOffGridLoad_StepsToTheNextLoadTheRackHas()
    {
        // Bučica od 15 kg na koraku od 2 kg, 3 x 12 @RIR2 u 8-12 @RIR1. Kapacitet je
        // proveravan za 17, a zaokruživanje 17 je davalo 18 kg (6.7 ponavljanja do otkaza,
        // dno je 8). Sledeća težina na mreži je 16.
        Assert.Equal(16m, Next(15m, 12, 2, 8, 12, 1, 2m));
    }

    [Theory]
    // Uska nedelja na laganoj šipci, sve do otkaza na 13: prva verzija ove izmene je
    // ponavljanja iznad opsega brojala dvaput i davala 32.5 kg, gde ostaje 9.7 ponavljanja.
    // Uzak propis ide na staro pravilo, a ono tu drži težinu.
    [InlineData(30, 0, 13, 0, true, 11, 12, 2, 2.5, 30)]
    // Fiksnih 5 @RIR1 na 60 kg, 6 do otkaza: manjak rezerve, staro pravilo drži.
    [InlineData(60, 0, 6, 0, true, 5, 5, 1, 2.5, 60)]
    // Propadanja sa 40 kg na 51.2 kg tela, 13 do otkaza u 11-12 @RIR1, korak 5: širok
    // opseg, kapacitet (43) ne upija korak.
    [InlineData(40, 51.2, 13, 0, true, 11, 12, 1, 5, 40)]
    [InlineData(40, 51.2, 12, 0, true, 11, 12, 1, 5, 40)]
    public void ASessionWhoseCapacityDoesNotAbsorbTheStep_NeverSteps_AndIsNeverLowered(
        double usedKg,
        double bodyKg,
        int reps,
        int rir,
        bool isFailure,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        double stepKg,
        double expectedKg)
    {
        var result = _engine.ComputeNext(
            (decimal)usedKg,
            Sets(reps, rir, isFailure),
            targetRir,
            repRangeMin,
            repRangeMax,
            (decimal)stepKg,
            (decimal)bodyKg);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    [Theory]
    // 5 x 5 @RIR1 na 80 kg, tačno po propisu: druga verzija ove izmene je uske propise
    // sudila po kapacitetu (2880 naspram 2887.5) i čučanj više nikada nije napredovao.
    [InlineData(80, 5, 1, false, 5, 5, 1, 2.5, 82.5)]
    [InlineData(40, 5, 2, false, 5, 5, 2, 2.5, 42.5)]
    [InlineData(40, 5, 0, true, 5, 5, 2, 2.5, 40)]
    // Fiksnih 12 na bučici od 8 kg: po kapacitetu ni RIR 5 nije dovoljan, pa je stajalo
    // zauvek. Staro pravilo korača - grub korak za takav propis ostaje ograničenje propisa.
    [InlineData(8, 12, 1, false, 12, 12, 1, 2, 10)]
    public void ANarrowPrescription_KeepsTheOldRule_AtEveryLoad(
        double usedKg,
        int reps,
        int rir,
        bool isFailure,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        double stepKg,
        double expectedKg)
    {
        var result = _engine.ComputeNext(
            (decimal)usedKg,
            Sets(reps, rir, isFailure),
            targetRir,
            repRangeMin,
            repRangeMax,
            (decimal)stepKg);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    [Theory]
    // Gde korak staje u propis, pravilo iz runda 9 i 10 važi nepromenjeno.
    [InlineData(100, 12, 0, true, 11, 12, 1, 102.5)]
    [InlineData(100, 12, 0, true, 11, 12, 2, 100)]
    [InlineData(100, 12, 0, true, 8, 12, 1, 102.5)]
    [InlineData(100, 5, 2, false, 5, 5, 2, 102.5)]
    public void WhereTheStepFitsThePrescription_TheOldRuleDecides(
        double usedKg,
        int reps,
        int rir,
        bool isFailure,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        double expectedKg)
    {
        var result = _engine.ComputeNext((decimal)usedKg, Sets(reps, rir, isFailure), targetRir, repRangeMin, repRangeMax, 2.5m);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    // --- korekcija koju korak ne može da izrazi ---

    [Theory]
    // Dumbbell 10 kg, 5/4/4 @RIR1 u 8-12: korekcija -10% daje 9, što se zaokruživalo na 10.
    [InlineData(10, 2, 8)]
    [InlineData(8, 2, 6)]
    // Šipka i sajla na 12.5, mašina na 25: ista granica, 10% je tačno pola koraka.
    [InlineData(12.5, 2.5, 10)]
    [InlineData(25, 5, 20)]
    // Iznad granice zaokruživanje korekciju već izražava: nepromenjeno ponašanje.
    [InlineData(30, 5, 25)]
    [InlineData(100, 2.5, 90)]
    // Težina od jednog koraka nema lakše: korak ispod bi bio prazna ruka, koju ekran
    // prijavljuje kao grešku. Revizija je našla predlog od 0 kg.
    [InlineData(2, 2, 2)]
    [InlineData(5, 5, 5)]
    [InlineData(2.5, 2.5, 2.5)]
    public void ACappedCorrectionThatRoundingErased_MovesTheLoadDownOneStep(
        double loadKg,
        double stepKg,
        double expectedKg)
    {
        var sets = new List<WorkingSet> { new(5, 1), new(4, 1), new(4, 1) };

        var result = _engine.ComputeNext((decimal)loadKg, sets, targetRir: 1, repRangeMin: 8, repRangeMax: 12, weightStepKg: (decimal)stepKg);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    [Fact]
    public void AnUncappedCorrectionThatRoundingErased_StillHoldsTheLoad()
    {
        // 7 @RIR0 ispod dna je otkaz jedno ponavljanje ispod opsega: odstupanje -2, -6%,
        // 9.4 kg se zaokružuje na 10. Korak naniže se daje samo na granici korekcije;
        // ovde vežbač gradi ponavljanja na istoj težini.
        Assert.Equal(10m, Next(10m, 7, 0, 8, 12, 1, 2m));
    }

    // --- deload ---

    [Theory]
    [InlineData(10, 0, 2, 8)]
    [InlineData(8, 0, 2, 6)]
    [InlineData(12.5, 0, 2.5, 10)]
    [InlineData(25, 0, 5, 20)]
    // Bez izmene gde 90% i dalje pada ispod pune težine.
    [InlineData(100, 0, 2.5, 90)]
    [InlineData(40, 0, 5, 35)]
    // Podignuto van mreže koraka: 90% od 11 je 9.9, zaokruženo 10 - lakše od 11, dovoljno.
    [InlineData(11, 0, 2.5, 10)]
    [InlineData(2.5, 0, 2, 2)]
    // Težina od jednog koraka nema lakše: korak ispod je prazna ruka, koju ekran s pravom
    // prijavljuje kao grešku. Deload tu ide kroz serije i rezervu. Revizija je našla 0 kg.
    [InlineData(2, 0, 2, 2)]
    [InlineData(5, 0, 5, 5)]
    [InlineData(2.5, 0, 2.5, 2.5)]
    // Nikad teže od podignutog: 9 kg na koraku od 10 se zaokruživalo na 10.
    [InlineData(9, 0, 10, 9)]
    // Lakše od koraka: 90% se zaokružuje na nulu, a spolja opterećena vežba ne ostaje prazna.
    [InlineData(1, 0, 2, 1)]
    [InlineData(2.5, 0, 5, 2.5)]
    // Samo telo, ništa dodato: nula dodatih je i puna težina i deload.
    [InlineData(80, 80, 1, 0)]
    public void DeloadLoad_IsLighterWheneverTheStepAllows_AndNeverHeavier(
        double fullTotalKg,
        double bodyweightKg,
        double stepKg,
        double expectedKg)
    {
        var deload = NextWeekLoad.DeloadLoad((decimal)fullTotalKg, (decimal)bodyweightKg, (decimal)stepKg);

        Assert.Equal((decimal)expectedKg, deload);
    }

    [Fact]
    public void DeloadLoad_OnEveryLoadAndStep_IsLighterWhenAStepBelowExists_AndNeverEmptiesALoadedLift()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var load in Enumerable.Range(1, 160).Select(k => k * step).Concat([1m, 9m, 11m, 41.5m, 83.1m]))
            {
                var deload = NextWeekLoad.DeloadLoad(load, 0m, step);

                if (deload > load)
                {
                    failures.Add($"{load}/{step}: deload {deload} heavier than the load");
                }

                if (WeightMath.StepBelow(load, step) > 0 && deload >= load)
                {
                    failures.Add($"{load}/{step}: deload {deload} not lighter");
                }

                if (deload <= 0)
                {
                    failures.Add($"{load}/{step}: deload empties a loaded lift");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(10)));
    }

    [Fact]
    public void TheNextWeekPath_UsesDeloadLoad_AlsoWhenTheLoadComesFromTheMaximum()
    {
        var fromReference = NextWeekLoad.For(
            referenceWeightKg: 10m,
            progressionWeightKg: null,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 3),
            nextIsDeload: true,
            oneRepMaxKg: null,
            weightStepKg: 2m);

        // Maksimum 13 kg: radna težina za 8 @RIR1 je 10 kg (13 / 1.3). Deload se meri od te
        // propisane težine, ne od nezaokruženog broja iz formule.
        var fromMaximum = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: new LoadPrescription(8, 12, 1),
            next: new LoadPrescription(8, 12, 3),
            nextIsDeload: true,
            oneRepMaxKg: 13m,
            weightStepKg: 2m);

        Assert.Equal(NextWeekLoad.DeloadLoad(10m, 0m, 2m), fromReference);
        Assert.Equal(8m, fromReference);
        Assert.Equal(8m, fromMaximum);
    }

    [Fact]
    public void TheDeloadFactor_IsAppliedInOnePlace()
    {
        // Nedelja posle završenog treninga i auto-deload (DeloadService) su nosili svaki svoju
        // kopiju 90%. Servis nema testni harness, pa se jedno pravilo čuva ovde: faktor sme da
        // se pomene samo u definiciji i u NextWeekLoad. Kada izvorni kod nije dostupan
        // (spakovan izlaz), test ćuti.
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("DeloadWeightFactor", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(name => name is not ("TrainingConstants.cs" or "NextWeekLoad.cs"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Faktor deload-a se primenjuje van NextWeekLoad: {string.Join(", ", offenders)}. " +
            "Koristi NextWeekLoad.DeloadLoad.");
    }

    [Theory]
    [InlineData(10, 2, 8)]
    [InlineData(9, 2, 8)]
    [InlineData(1, 2, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(100, 2.5, 97.5)]
    public void StepBelow_IsTheGridPointStrictlyBelow(double value, double step, double expected)
    {
        Assert.Equal((decimal)expected, WeightMath.StepBelow((decimal)value, (decimal)step));
    }

    [Theory]
    [InlineData(10, 2, 12)]
    [InlineData(9, 2, 10)]
    [InlineData(15, 2, 16)]
    [InlineData(22.5, 5, 25)]
    [InlineData(100, 2.5, 102.5)]
    public void StepAbove_IsTheNextGridPoint_NeverMoreThanOneStepAway(double value, double step, double expected)
    {
        Assert.Equal((decimal)expected, WeightMath.StepAbove((decimal)value, (decimal)step));
    }

    private decimal Next(
        decimal usedKg,
        int reps,
        int rir,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        decimal stepKg,
        bool isFailure = false)
    {
        return _engine
            .ComputeNext(usedKg, Sets(reps, rir, isFailure), targetRir, repRangeMin, repRangeMax, stepKg)
            .NextWeightKg;
    }

    private static List<WorkingSet> Sets(int reps, int rir, bool isFailure = false)
    {
        return [new WorkingSet(reps, rir, isFailure), new WorkingSet(reps, rir, isFailure), new WorkingSet(reps, rir, isFailure)];
    }

    private static bool FloorReachable(int reps, int rir, decimal load, decimal step, int min)
    {
        return (30m + reps + rir) * load >= (load + step) * (30m + min);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StrengthPlanner.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
