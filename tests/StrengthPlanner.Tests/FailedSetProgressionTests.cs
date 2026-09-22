using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Otkaz proširuje RIR skalu ispod nule i time izjednačava korekciju naniže sa
/// korekcijom naviše, što je ograničenje opisano u zaključku rada.
/// </summary>
public class FailedSetProgressionTests
{
    private readonly ProgressionEngine _engine = new();

    [Theory]
    // Bez otkaza RIR ostaje onakav kakav je unet.
    [InlineData(10, 2, false, 8, 2)]
    // Otkaz na dnu opsega: nije promašeno nijedno ponavljanje -> RIR 0.
    [InlineData(8, 0, true, 8, 0)]
    // Otkaz iznad dna opsega je i dalje 0, ne pozitivan broj.
    [InlineData(11, 0, true, 8, 0)]
    // Otkaz ispod dna opsega: svako promašeno ponavljanje je jedan RIR poen naniže.
    [InlineData(6, 0, true, 8, -2)]
    [InlineData(3, 0, true, 8, -5)]
    // Prijavljeno iz stvarne upotrebe: 6 od 8-12, RIR 0, BEZ kvačice. Nula rezerve ispod
    // dna opsega je otkaz po definiciji bez obzira na IsFailure - identičan rezultat kao
    // gornji red sa istim brojevima i kvačicom uključenom.
    [InlineData(6, 0, false, 8, -2)]
    [InlineData(3, 0, false, 8, -5)]
    // Ispod dna opsega meri se kapacitet: ponavljanja + RIR prema donjoj granici. Razlog
    // zbog kog je vežbač stao ne menja šta brojevi govore o opterećenju.
    // 6 sa RIR 2 = kapacitet 8, tačno na dnu.
    [InlineData(6, 2, false, 8, 0)]
    // 5 sa RIR 2 = kapacitet 7, jedno ispod dna - isto kao otkaz na 7.
    [InlineData(5, 2, false, 8, -1)]
    [InlineData(7, 1, false, 8, 0)]
    // Velika rezerva ispod dna i dalje može da bude "lakše od plana".
    [InlineData(6, 3, false, 8, 1)]
    [InlineData(5, 5, false, 8, 2)]
    [InlineData(3, 1, false, 8, -4)]
    // RIR 0 na dnu ili iznad njega bez kvačice i dalje znači "jedva sam stigao, ali jesam" -
    // ne otkaz. Ponašanje pre ove izmene, i dalje nepromenjeno.
    [InlineData(8, 0, false, 8, 0)]
    [InlineData(12, 0, false, 8, 0)]
    public void EffectiveRir_CountsMissedRepsAsNegativeRir(
        int reps,
        int rir,
        bool isFailure,
        int repRangeMin,
        int expected)
    {
        var set = new WorkingSet(reps, rir, isFailure);

        Assert.Equal(expected, set.EffectiveRir(repRangeMin));
    }

    /// <summary>
    /// <see cref="WorkingSet.ImpliesFailure"/> je javna i deljena namerno: infrastrukturni
    /// sloj koji upisuje <c>SetLog.IsFailure</c> u bazu poziva istu metodu, da definicija
    /// "šta je otkaz" ne postoji na dva mesta koja mogu da se razmimoiđu.
    /// </summary>
    [Theory]
    [InlineData(6, 0, 8, false, true)] // prijavljeni slučaj: nula rezerve ispod dna
    [InlineData(8, 0, 8, false, false)] // nula rezerve TAČNO na dnu - "jedva sam stigao"
    [InlineData(12, 0, 8, false, false)] // nula rezerve iznad dna - isto tako
    [InlineData(6, 1, 8, false, false)] // rezerva iznad nule ispod dna - namerno stao
    [InlineData(6, 0, 8, true, true)] // eksplicitna kvačica i dalje radi
    [InlineData(12, 5, 8, true, true)] // eksplicitna kvačica nadjačava brojeve
    public void ImpliesFailure_MatchesTheRuleEffectiveRirApplies(
        int reps,
        int rir,
        int repRangeMin,
        bool explicitlyMarked,
        bool expected)
    {
        Assert.Equal(expected, WorkingSet.ImpliesFailure(reps, rir, repRangeMin, explicitlyMarked));
    }

    [Fact]
    public void ComputeNext_CorrectsFurtherDown_WhenSetsFailShortOfRange()
    {
        // Tri otkaza na 5 ponavljanja uz opseg 8-12: promašena su po 3 ponavljanja,
        // pa je efektivni RIR -3, odstupanje od cilja -4 poena => -12% ograničeno na -10%.
        var failed = new List<WorkingSet>
        {
            new(5, 0, IsFailure: true),
            new(5, 0, IsFailure: true),
            new(5, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(
            usedWeightKg: 100m,
            failed,
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12);

        Assert.Equal(90m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_ReachesSameCapDownwardAsUpward()
    {
        // Simetrija koju rad navodi kao nedostatak: pre otkaza je najveća korekcija
        // naniže bila -3% (RIR 0 uz cilj 1), a naviše punih +10%.
        var tooEasy = new List<WorkingSet> { new(8, 5), new(8, 5), new(8, 5) };
        var tooHard = new List<WorkingSet>
        {
            new(4, 0, IsFailure: true),
            new(4, 0, IsFailure: true),
            new(4, 0, IsFailure: true)
        };

        var up = _engine.ComputeNext(100m, tooEasy, targetRir: 1, repRangeMin: 8, repRangeMax: 12);
        var down = _engine.ComputeNext(100m, tooHard, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(110m, up.NextWeightKg);
        Assert.Equal(90m, down.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_WithoutFailure_KeepsNarrowDownwardCorrection()
    {
        // RIR 0 bez otkaza i dalje znači "jedva sam stigao", ne "nisam uspeo":
        // korekcija ostaje -3%, kao i pre ove izmene.
        var hardButCompleted = new List<WorkingSet> { new(8, 0), new(8, 0), new(8, 0) };

        var result = _engine.ComputeNext(100m, hardButCompleted, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(97.5m, result.NextWeightKg);
    }

    /// <summary>
    /// Prijavljeno iz stvarne upotrebe: korisnik je pitao zašto kvačica "Serija do
    /// otkaza" uopšte postoji kad je RIR 0 već jasan signal. Ovaj test dokazuje da RIR 0
    /// ispod dna opsega SADA daje istu korekciju bez obzira da li je kvačica dotaknuta -
    /// zaboravljena kvačica više ne menja koliko opterećenje pada.
    /// </summary>
    [Fact]
    public void ComputeNext_AppliesTheSameCorrection_WhetherOrNotFailureWasChecked()
    {
        var withoutCheckbox = new List<WorkingSet> { new(5, 0, IsFailure: false) };
        var withCheckbox = new List<WorkingSet> { new(5, 0, IsFailure: true) };

        var a = _engine.ComputeNext(100m, withoutCheckbox, targetRir: 1, repRangeMin: 8, repRangeMax: 12);
        var b = _engine.ComputeNext(100m, withCheckbox, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(b.NextWeightKg, a.NextWeightKg);
        Assert.True(a.NextWeightKg < 100m);
    }

    [Fact]
    public void ComputeNext_StillAddsWeight_WhenTopOfRangeWasReachedByFailing()
    {
        // Otkaz NA vrhu opsega ne poništava double progression: vrh opsega je upravo
        // signal na kome progresija počiva. Sledeći trening kreće od dna opsega (8), a to
        // su četiri ponavljanja rezerve - manjak od jednog RIR poena je time već plaćen, pa
        // se negativna korekcija na vrhu ne primenjuje dok je manjak manji od širine opsega.
        var failedAtTop = new List<WorkingSet>
        {
            new(12, 0, IsFailure: true),
            new(12, 0, IsFailure: true),
            new(12, 0, IsFailure: true)
        };
        var comfortableAtTop = new List<WorkingSet> { new(12, 1), new(12, 1), new(12, 1) };

        var failed = _engine.ComputeNext(100m, failedAtTop, targetRir: 1, repRangeMin: 8, repRangeMax: 12);
        var comfortable = _engine.ComputeNext(100m, comfortableAtTop, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.True(failed.WeightIncreased);
        // Ceo korak: 100 + 2.5. Ranije je ovde stajalo 100 * 0.97 + 2.5 = 99.5 -> 100,
        // "zadržava se" - ali samo zato što je 100 kg slučajno u pojasu 42-125 kg.
        Assert.Equal(102.5m, failed.NextWeightKg);
        Assert.Equal(102.5m, comfortable.NextWeightKg);
    }

    /// <summary>
    /// Regresija iz runde 1: raniji pokušaj da se otkaz kazni i preko double progression-a
    /// gurao je opterećenje naniže iz treninga u trening (100 -> 80 kg za osam treninga)
    /// iako je vežbač svaki put stizao do vrha opsega.
    ///
    /// Test koji je to trebalo da čuva počinjao je od 100 kg i tako prolazio slučajno:
    /// formula 0.97u + korak drži opterećenje samo između ~42 i 125 kg (za šipku), a iznad
    /// toga ga obara - 160 kg je za osam treninga pao na 140. Zato se sada proverava mreža
    /// težina i koraka, i svaki trening posebno, ne samo krajnja vrednost.
    /// </summary>
    [Theory]
    [InlineData(100.0, 2.5, 8, 12, 1, 12, 0, true, 120.0)]
    [InlineData(160.0, 2.5, 8, 12, 1, 12, 0, true, 180.0)]
    [InlineData(300.0, 2.5, 8, 12, 1, 12, 0, true, 320.0)]
    [InlineData(180.0, 2.5, 3, 6, 2, 6, 1, false, 200.0)]
    [InlineData(110.0, 2.0, 8, 12, 1, 12, 0, true, 126.0)]
    [InlineData(260.0, 5.0, 8, 12, 1, 12, 0, true, 300.0)]
    [InlineData(20.0, 0.5, 8, 12, 1, 12, 0, true, 24.0)]
    // Uska nedelja (11-12 sa RIR 2) izvučena do otkaza: po Epley-u sledeći propis ne ide
    // teže, pa se težina drži - ali nikad ne pada.
    [InlineData(100.0, 2.5, 11, 12, 2, 12, 0, true, 100.0)]
    public void ComputeNext_NeverLowersLoad_WhenEveryTopOfRangeSessionEndsTheSameWay(
        double startKg,
        double stepKg,
        int repRangeMin,
        int repRangeMax,
        int targetRir,
        int reps,
        int rir,
        bool isFailure,
        double expectedFinalKg)
    {
        var weightKg = (decimal)startKg;

        for (var session = 0; session < 8; session++)
        {
            var sets = new List<WorkingSet>
            {
                new(reps, rir, isFailure),
                new(reps, rir, isFailure),
                new(reps, rir, isFailure)
            };

            var next = _engine
                .ComputeNext(weightKg, sets, targetRir, repRangeMin, repRangeMax, (decimal)stepKg)
                .NextWeightKg;

            Assert.True(next >= weightKg, $"Session {session + 1}: {weightKg} kg -> {next} kg.");
            weightKg = next;
        }

        Assert.Equal((decimal)expectedFinalKg, weightKg);
    }

    [Fact]
    public void ComputeNext_LowersLoad_WhenRepsFellBelowRangeWithReserve()
    {
        // Prijavljeno u pregledu logike: 3x5 sa RIR 2 u opsegu 8-12 davalo je 102.5 kg -
        // teže - jer se RIR 2 čitao kao "lakše od plana". Kapacitet je 5 + 2 = 7, jedno
        // ispod dna: odstupanje -2 poena, -6% => 94 -> 95 kg. Epley daje isto:
        // 100 * (1 + 7/30) / (1 + 9/30) = 94.9 kg.
        var sets = new List<WorkingSet> { new(5, 2), new(5, 2), new(5, 2) };

        var result = _engine.ComputeNext(100m, sets, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal(95m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }

    [Theory]
    // Kapacitet 5 + 4 = 9 = dno + cilj: tačno propisano, težina se ne menja.
    [InlineData(5, 4, 100.0)]
    // Kapacitet 10: i ispod opsega, opterećenje koje je stvarno lako i dalje raste.
    [InlineData(5, 5, 102.5)]
    // Isti kapacitet (9) ispod i unutar opsega daje isti predlog.
    [InlineData(6, 3, 100.0)]
    [InlineData(8, 1, 100.0)]
    public void ComputeNext_JudgesBelowFloorSetsByCapacity(int reps, int rir, double expectedKg)
    {
        var sets = new List<WorkingSet> { new(reps, rir), new(reps, rir), new(reps, rir) };

        var result = _engine.ComputeNext(100m, sets, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.Equal((decimal)expectedKg, result.NextWeightKg);
    }

    [Fact]
    public void ComputeNext_DoesNotAddWeight_WhenAnySetFailedShortOfTheTop()
    {
        // Otkaz ISPOD vrha opsega i dalje blokira korak — ali kroz allHitTop,
        // jer serija koja nije stigla do vrha po definiciji obara taj uslov.
        var failedShort = new List<WorkingSet>
        {
            new(12, 1),
            new(12, 1),
            new(9, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(100m, failedShort, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        Assert.False(result.WeightIncreased);
    }

    [Fact]
    public void ComputeNext_AveragesFailedAndCompletedSetsTogether()
    {
        // Prva serija prošla sa RIR 2, poslednja otkazala 3 ponavljanja ispod opsega:
        // prosek efektivnog RIR-a je (2 + 0 + (-3)) / 3 = -1/3, odstupanje -4/3 poena.
        var mixed = new List<WorkingSet>
        {
            new(10, 2),
            new(9, 0),
            new(5, 0, IsFailure: true)
        };

        var result = _engine.ComputeNext(100m, mixed, targetRir: 1, repRangeMin: 8, repRangeMax: 12);

        // -1.3333 * 3% = -4% => 96 kg, zaokruženo na 2.5 kg.
        Assert.Equal(95m, result.NextWeightKg);
        Assert.False(result.WeightIncreased);
    }
}
