using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Deload je do sada bio vezan za kalendar (četvrta nedelja). Ovi testovi pokrivaju
/// ocenu umora koja može da ga povuče ranije.
/// </summary>
public class FatigueEvaluatorTests
{
    /// <summary>Nedelja hipertrofije (ciljni RIR 1) koja je prošla tačno po planu.</summary>
    // Pad snage se od runde 15 računa tek kad ga potvrdi i prethodna nedelja. Pomoćne
    // funkcije zato zadaju isto čitanje za obe nedelje, osim kad test kaže drugačije, pa
    // testovi pisani za jedno čitanje zadržavaju značenje.
    private static WeeklyFatigue Hypertrophy(
        decimal rirDeviation = 0m,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m,
        decimal? previousE1RmChange = null) =>
        new(rirDeviation, AchievableRirDeficit: 1m, failureShare, e1RmChange, volumeShare, previousE1RmChange ?? e1RmChange);

    /// <summary>Nedelja snage (ciljni RIR 2).</summary>
    private static WeeklyFatigue Strength(
        decimal rirDeviation = 0m,
        decimal failureShare = 0m,
        decimal e1RmChange = 0m,
        decimal volumeShare = 0m,
        decimal? previousE1RmChange = null) =>
        new(rirDeviation, AchievableRirDeficit: 2m, failureShare, e1RmChange, volumeShare, previousE1RmChange ?? e1RmChange);

    [Fact]
    public void Score_IsZero_ForAWeekThatWentToPlan()
    {
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy()));
    }

    [Fact]
    public void Score_IsOne_WhenEverySignalIsMaxedOut()
    {
        var wrecked = Hypertrophy(
            rirDeviation: -3m,
            failureShare: 1m,
            e1RmChange: -0.20m,
            volumeShare: 1.5m);

        Assert.Equal(1m, FatigueEvaluator.Score(wrecked));
    }

    [Fact]
    public void Score_IgnoresSignalsPointingTheOtherWay()
    {
        // Nedelja lakša od plana, sa rastom snage i malim volumenom: pozitivni signali
        // ne smeju da daju negativnu ocenu niti da "kompenzuju" nešto drugo.
        var easy = Hypertrophy(rirDeviation: 2m, e1RmChange: 0.08m, volumeShare: 0.3m);

        Assert.Equal(0m, FatigueEvaluator.Score(easy));
    }

    [Fact]
    public void Score_TreatsGoalsWithDifferentTargetRirTheSame()
    {
        // Vežbač koji je celu nedelju grebao dno svoje skale (RIR 0 uz cilj 1, odnosno
        // uz cilj 2) mora dobiti isti signal — inače hipertrofija, kao podrazumevani
        // cilj, nikada ne bi mogla da iskoristi ovaj udeo.
        var hypertrophyAtZeroRir = Hypertrophy(rirDeviation: -1m);
        var strengthAtZeroRir = Strength(rirDeviation: -2m);

        Assert.Equal(
            FatigueEvaluator.Score(strengthAtZeroRir),
            FatigueEvaluator.Score(hypertrophyAtZeroRir));
    }

    [Fact]
    public void ShouldDeload_TriggersForHypertrophy_WhenTheWeekGrindsAndStrengthDrops()
    {
        // Regresija: dok se odstupanje merilo fiksnom skalom od dva poena, hipertrofija
        // (ciljni RIR 1) nije mogla da pređe prag ni sa svim ostalim signalima na
        // maksimumu — najviše 0.575 od potrebnih 0.60.
        var grinding = Hypertrophy(rirDeviation: -1m, e1RmChange: -0.05m, volumeShare: 1m);

        Assert.True(FatigueEvaluator.ShouldDeload(grinding));
    }

    [Theory]
    // Nijedan pojedinačni signal ne sme sam da pređe prag: najteži nosi 0.35, prag je 0.60.
    [InlineData(-3, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, -0.20, 0)]
    [InlineData(0, 0, 0, 1.5)]
    public void ShouldDeload_IsFalse_WhenOnlyOneSignalIsMaxedOut(
        double rirDeviation,
        double failureShare,
        double e1RmChange,
        double volumeShare)
    {
        var fatigue = Hypertrophy(
            (decimal)rirDeviation,
            (decimal)failureShare,
            (decimal)e1RmChange,
            (decimal)volumeShare);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void Score_KeepsTheRirAndFailureSignalsIndependent()
    {
        // Otkazi ulaze isključivo kroz FailureShare. Da su ulazili i u prosek RIR-a,
        // jedan te isti događaj bi popunio oba najteža signala i "moraju se složiti bar
        // dva" ne bi značilo ništa.
        var completedSetsWentToPlan = Hypertrophy(rirDeviation: 0m, failureShare: 0.5m);

        // 0.25 od maksimalnog udela za otkaze, ništa od RIR-a.
        Assert.Equal(0.25m, FatigueEvaluator.Score(completedSetsWentToPlan));
        Assert.False(FatigueEvaluator.ShouldDeload(completedSetsWentToPlan));
    }

    /// <summary>
    /// Nedelja u kojoj nijedna serija nije dovršena nosi tu činjenicu kroz udeo otkaza, i
    /// samo kroz njega. Ranije je ista činjenica dizala i RIR signal na najgore očitavanje
    /// (1.0), pa je 0.35 + 0.25 davalo tačno prag — jedan uzrok je pokretao deload, u
    /// pravilu koje kaže da nijedan signal to ne može sam.
    /// </summary>
    [Fact]
    public void Score_ReadsAWeekWithoutACompletedSet_ThroughTheFailureShareAlone()
    {
        var everySetFailed = Hypertrophy(rirDeviation: 0m, failureShare: 1m);

        Assert.Equal(0.25m, FatigueEvaluator.Score(everySetFailed));
        Assert.False(FatigueEvaluator.ShouldDeload(everySetFailed));
    }

    /// <summary>
    /// Ista nedelja sa još jednim stvarnim signalom i dalje pokreće deload. To je razlika
    /// između "sve je išlo do otkaza" i "sve je išlo do otkaza, a snaga je pala".
    /// </summary>
    [Fact]
    public void ADeload_StillFollows_WhenASecondSignalAgrees()
    {
        Assert.Equal(0.50m, FatigueEvaluator.Score(Hypertrophy(failureShare: 1m, e1RmChange: -0.05m)));
        Assert.Equal(0.40m, FatigueEvaluator.Score(Hypertrophy(failureShare: 1m, volumeShare: 1m)));
        Assert.True(FatigueEvaluator.ShouldDeload(
            Hypertrophy(failureShare: 1m, e1RmChange: -0.05m, volumeShare: 1m)));
    }

    /// <summary>
    /// Merenje koje je porušilo staro pravilo: jedna serija od dvadeset je pomerala ocenu
    /// za 0.35 i odlučivala o deload-u. Dvadeset otkaza je davalo 0.60, a devetnaest otkaza
    /// uz jednu dovršenu seriju na cilju 0.25 — litica, a ne mera.
    /// </summary>
    [Fact]
    public void OneCompletedSet_NoLongerSwingsTheScoreByATerm()
    {
        var everySetFailed = Hypertrophy(rirDeviation: 0m, failureShare: 1m);
        var oneSetCompleted = Hypertrophy(rirDeviation: 0m, failureShare: 19m / 20m);

        var gap = FatigueEvaluator.Score(everySetFailed) - FatigueEvaluator.Score(oneSetCompleted);

        // Tacno nula, a ne samo malo: udeo otkaza dostize punu tezinu na 0.5, pa i 0.95 i
        // 1.0 nose isti maksimum. Ranije je ta jedna serija menjala ocenu za 0.35 i sama
        // odlucivala o deload-u.
        Assert.Equal(0m, gap);
    }

    [Fact]
    public void ShouldDeload_IsFalse_ForAHardButProductiveWeek()
    {
        // Naporna nedelja u kojoj snaga i dalje raste nije razlog za deload;
        // to je upravo nedelja zbog koje se trenira.
        var fatigue = Hypertrophy(
            rirDeviation: -1m,
            failureShare: 0.2m,
            e1RmChange: 0.02m,
            volumeShare: 0.85m);

        Assert.False(FatigueEvaluator.ShouldDeload(fatigue));
    }

    [Fact]
    public void Score_TreatsMissingE1RmComparisonAsNeutral()
    {
        // Prva nedelja nema sa čim da se poredi; nedostatak podatka ne sme da se
        // protumači kao pad performansi.
        var withoutComparison = Hypertrophy(rirDeviation: -0.5m, failureShare: 0.2m, volumeShare: 0.85m);
        var withDrop = withoutComparison with { E1RmChangeShare = -0.05m, PreviousE1RmChangeShare = -0.05m };

        Assert.True(FatigueEvaluator.Score(withDrop) > FatigueEvaluator.Score(withoutComparison));
    }

    [Fact]
    public void Score_DoesNotCountVolumeBelowEightyPercentOfMrv()
    {
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy(volumeShare: 0.5m)));
        Assert.Equal(0m, FatigueEvaluator.Score(Hypertrophy(volumeShare: 0.8m)));
    }

    [Fact]
    public void Score_NeverLeavesTheZeroToOneRange()
    {
        var extremes = new[]
        {
            new WeeklyFatigue(-100m, 1m, 5m, -5m, 10m, PreviousE1RmChangeShare: -5m),
            new WeeklyFatigue(100m, 1m, -5m, 5m, -10m, PreviousE1RmChangeShare: 5m),
            new WeeklyFatigue(0m, 0m, 0m, 0m, 0m, PreviousE1RmChangeShare: 0m),
            new WeeklyFatigue(-1m, -3m, 0m, 0m, 0m, PreviousE1RmChangeShare: 0m)
        };

        foreach (var fatigue in extremes)
        {
            var score = FatigueEvaluator.Score(fatigue);

            Assert.InRange(score, 0m, 1m);
        }
    }

    /// <summary>
    /// Ista funkcija sada služi i granicama volumena, pa prima ponder po seriji (doprinos
    /// mišiću × blizina otkaza). Važno je da ponder ne vraća otkaze u prosek: granice su
    /// računale SVOJU verziju nad svim serijama, i na ove tri je dobijala −2 tamo gde ocena
    /// umora dobija 0 — pa je jedan otkaz zatvarao oba uslova I-testa "imao je rezerve".
    /// </summary>
    [Fact]
    public void AverageRirDeviation_TakesAWeight_AndStillLeavesFailuresOut()
    {
        RirSample[] sets =
        [
            new(new WorkingSet(10, 1), 8, 1, Weight: 1m),
            new(new WorkingSet(10, 1), 8, 1, Weight: 0.5m),
            new(new WorkingSet(3, 0, IsFailure: true), 8, 1, Weight: 1m)
        ];

        Assert.Equal(0m, FatigueEvaluator.AverageRirDeviation(sets));

        // Stara računica granica volumena, ostavljena kao oracle: prosek nad SVIM serijama.
        var overEverySet = sets.Average(sample =>
            (decimal)(sample.Set.EffectiveRir(sample.RepRangeMin) - sample.TargetRir));

        Assert.Equal(-2m, overEverySet);
    }

    /// <summary>Ponder zaista pomera prosek, i to u odnosu na svoju sumu, ne na broj serija.</summary>
    [Fact]
    public void AverageRirDeviation_LeansTowardTheHeavierSet()
    {
        RirSample[] sets =
        [
            new(new WorkingSet(10, 3), 8, 1, Weight: 3m),
            new(new WorkingSet(10, 1), 8, 1, Weight: 1m)
        ];

        // (3 × 2 + 1 × 0) / 4
        Assert.Equal(1.5m, FatigueEvaluator.AverageRirDeviation(sets));
    }

    [Fact]
    public void AverageRirDeviation_ReadsABelowFloorSetWithReserveAsHarder()
    {
        // 5 ponavljanja sa RIR 2 u 8-12 @RIR1: kapacitet 7, jedno ispod dna. Progresiji je
        // to -2 poena; sirov RIR bi ovde rekao +1, "lakše od plana".
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 2), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(-2m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_KeepsLoggedRirInsideTheRange()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(10, 0), RepRangeMin: 8, TargetRir: 1),
            new RirSample(new WorkingSet(9, 3), RepRangeMin: 8, TargetRir: 1)
        ]);

        // (-1 + 2) / 2
        Assert.Equal(0.5m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_LeavesFailuresToTheFailureShare()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 0, IsFailure: true), RepRangeMin: 8, TargetRir: 1),
            new RirSample(new WorkingSet(10, 2), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(1m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_CountsAnUnflaggedImpliedFailureAmongTheCompletedSets()
    {
        // Domenska funkcija ne zna za zastavicu iz baze: dobija ono što joj pozivalac
        // preda. SetLogService danas upisuje IsFailure = true i kad kvačica nije dotaknuta
        // (ImpliesFailure), ali serije upisane pre te izmene i dalje mogu da stoje sa
        // false. Takva serija ne ulazi u udeo otkaza, koji čita zastavicu, pa mora da
        // ostane u proseku RIR-a - inače nedelja u kojoj je vežbač promašio opseg ne bi
        // imala nijedan signal umora.
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(6, 0), RepRangeMin: 8, TargetRir: 1)
        ]);

        // Kapacitet 6 naspram dna 8 => -2, minus cilj 1.
        Assert.Equal(-3m, deviation);
    }

    [Fact]
    public void AverageRirDeviation_IsZero_WhenEverySetFailed()
    {
        var deviation = FatigueEvaluator.AverageRirDeviation([
            new RirSample(new WorkingSet(5, 0, IsFailure: true), RepRangeMin: 8, TargetRir: 1)
        ]);

        Assert.Equal(0m, deviation);
    }
    // --- runda 15: pad snage se računa tek kad ga potvrdi i prethodna nedelja ---

    /// <summary>
    /// Jedan slab dan spušta i RIR i procenu snage, pa "dva signala se slažu" bez potvrde
    /// nije bilo dva nezavisna signala. Nedelja koja škripi na MRV-u uz pad od 5% pokreće
    /// deload samo ako je i prethodna nedelja bila pad.
    /// </summary>
    [Theory]
    [InlineData(0.01, false)]
    [InlineData(0.0, false)]
    // Pad manji od praga granica volumena (1%) nije pad, nego ravna nedelja.
    [InlineData(-0.005, false)]
    [InlineData(-0.01, true)]
    [InlineData(-0.03, true)]
    public void AStrengthDrop_CountsOnlyWhenThePreviousWeekDeclinedToo(double previous, bool deload)
    {
        var week = Hypertrophy(rirDeviation: -1m, e1RmChange: -0.05m, volumeShare: 1m, previousE1RmChange: (decimal)previous);

        Assert.Equal(deload, FatigueEvaluator.ShouldDeload(week));
        Assert.Equal(deload ? 0.75m : 0.50m, FatigueEvaluator.Score(week));
    }

    [Fact]
    public void AConfirmedDrop_KeepsThisWeeksSize()
    {
        // Prethodna nedelja samo potvrđuje smer; veličinu nosi tekuće čitanje.
        var week = Hypertrophy(e1RmChange: -0.025m, previousE1RmChange: -0.10m);

        Assert.Equal(0.025m, FatigueEvaluator.ConfirmedStrengthDrop(week));
        Assert.Equal(0.125m, FatigueEvaluator.Score(week));
    }

    /// <summary>
    /// Zašto je pravilo promenjeno, kao merenje. Model šuma je onaj iz granica volumena
    /// (runda 14): nivo snage po nedelji ima sd 2.47%, pa čitanje (razlika dve nedelje) ima
    /// sd oko 3.5%. Vežbač drži zadat broj ponavljanja i iskreno prijavljuje rezervu, pa slab
    /// dan spušta RIR za jedno ponavljanje na svakih 2.4% snage (Epley za 8-12), uz grešku
    /// procene sd 0.22 nad nedeljom. Stvaran pad snage se oseti i u RIR-u, jer opterećenje
    /// dolazi iz prethodne nedelje; napredak ne, jer ga progresija prati.
    ///
    /// Struktura je periodizovan blok od šest nedelja: 1. nedelja nema čitanje, a ocena deluje
    /// posle 2., 3. i 4. (posle 5. je sledeća već deload). Udeo blokova u kojima ocena povuče
    /// deload, isto kao u docs/simulations/fatigue_signal_noise.py:
    /// - vežbač koji napreduje 1% nedeljno, nedelje na MAV-u: jedno čitanje ~12.5%, potvrda ~0.3%;
    /// - isti, nedelje na MRV-u: ~53% naspram ~3%;
    /// - stvaran pad od 3% nedeljno na MRV-u: ~98% naspram ~55% - ređe, ali i dalje se hvata.
    /// </summary>
    [Fact]
    public void UnderRealisticNoise_AProgressingLifterIsRarelyDeloaded_AndADecliningOneStillIs()
    {
        Assert.True(DeloadedBlocks(trend: 0.01m, volumeShare: 0.75m, confirmed: false) > 0.08m);
        Assert.True(DeloadedBlocks(trend: 0.01m, volumeShare: 0.75m, confirmed: true) < 0.02m);
        Assert.True(DeloadedBlocks(trend: 0.01m, volumeShare: 1m, confirmed: false) > 0.40m);
        Assert.True(DeloadedBlocks(trend: 0.01m, volumeShare: 1m, confirmed: true) < 0.08m);
        Assert.True(DeloadedBlocks(trend: -0.03m, volumeShare: 1m, confirmed: false) > 0.90m);
        Assert.True(DeloadedBlocks(trend: -0.03m, volumeShare: 1m, confirmed: true) > 0.40m);
    }

    private static decimal DeloadedBlocks(decimal trend, decimal volumeShare, bool confirmed)
    {
        const int Blocks = 3000;
        const double LevelSd = 0.0247;
        const double LoadPerRep = 0.024;
        int[] actionableWeeks = [2, 3, 4];
        var random = new Random(15);
        double Gauss(double sd) => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble()) * sd;

        var deloaded = 0;

        for (var block = 0; block < Blocks; block++)
        {
            var lastLevel = Gauss(LevelSd);
            var previousReading = 0m;
            var hit = false;

            for (var week = 1; week <= actionableWeeks.Max(); week++)
            {
                var level = Gauss(LevelSd);
                var change = level - lastLevel;
                lastLevel = level;

                // Prva nedelja bloka nema sa čim da se poredi.
                var reading = week == 1 ? 0m : trend + (decimal)change;
                var rirDeviation = (decimal)(change / LoadPerRep + Gauss(0.22))
                                   + (trend < 0 ? trend / (decimal)LoadPerRep : 0m);

                var fatigue = Hypertrophy(
                    rirDeviation: rirDeviation,
                    e1RmChange: reading,
                    volumeShare: volumeShare,
                    // Staro pravilo je svaki pad brojalo odmah, što je isto što i prethodna
                    // nedelja koja uvek potvrđuje.
                    previousE1RmChange: confirmed ? previousReading : -1m);

                hit |= actionableWeeks.Contains(week) && FatigueEvaluator.ShouldDeload(fatigue);
                previousReading = reading;
            }

            deloaded += hit ? 1 : 0;
        }

        return (decimal)deloaded / Blocks;
    }
}
