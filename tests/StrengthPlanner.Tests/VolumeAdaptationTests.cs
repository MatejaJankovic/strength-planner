using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Seed MEV/MAV/MRV vrednosti su populacioni prosek; ovi testovi pokrivaju kako se
/// pomeraju ka onome što konkretan korisnik stvarno podnosi.
/// </summary>
public class VolumeAdaptationTests
{
    // Chest iz seed-a: MEV 10, MAV 16, MRV 22.
    private static readonly VolumeLandmarkValues Seed = new(Mev: 10, Mav: 16, Mrv: 22);

    [Fact]
    public void Adjust_RaisesMrv_WhenNearMaxVolumeStillLeftRepsInReserve()
    {
        // 21 serija je iznad 90% MRV-a, a odstupanje RIR-a je ceo poen naviše:
        // korisnik podnosi više nego što populaciona granica pretpostavlja.
        var response = new VolumeResponse(PerformedSets: 21m, RawSets: 21m, AverageRirDeviation: 1m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(23, result.Mrv);
        Assert.Equal(10, result.Mev);
    }

    [Fact]
    public void Adjust_DoesNotRaiseMrv_WhenWeekWasOnlyMarginallyEasierThanPrescribed()
    {
        // Ispod celog RIR poena razlika je šum procene. Prag mora da važi u oba smera,
        // inače bi se MRV penjao i na nedeljama koje su u proseku bile teže od plana.
        var response = new VolumeResponse(PerformedSets: 21m, RawSets: 21m, AverageRirDeviation: 0.2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mrv, result.Mrv);
    }

    [Fact]
    public void Adjust_StillRaisesMrv_WhenOnlyTheLastSetWentToFailure()
    {
        // Poslednja serija do otkaza je uobičajena praksa; da nulti otkaz bude uslov,
        // gornja granica takvom vežbaču ne bi mogla nikada da poraste.
        var response = new VolumeResponse(PerformedSets: 21m, RawSets: 21m, AverageRirDeviation: 1.5m, FailureShare: 0.05m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(23, result.Mrv);
    }

    [Fact]
    public void Adjust_LowersMrv_WhenMeaningfulVolumeProducedFatigue()
    {
        var response = new VolumeResponse(PerformedSets: 16m, RawSets: 16m, AverageRirDeviation: -1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(21, result.Mrv);
    }

    [Fact]
    public void Adjust_LowersMrv_WhenAQuarterOfSetsEndedInFailure()
    {
        // Otkazi su signal umora i kada je prosečan RIR u redu.
        var response = new VolumeResponse(PerformedSets: 16m, RawSets: 16m, AverageRirDeviation: 0m, FailureShare: 0.3m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(21, result.Mrv);
    }

    [Fact]
    public void Adjust_LeavesMrvAlone_WhenEasyWeekWasFarBelowTheLimit()
    {
        // Lakoća na 12 serija ne dokazuje da bi i 22 bile podnošljive.
        var response = new VolumeResponse(PerformedSets: 12m, RawSets: 12m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(22, result.Mrv);
    }

    /// <summary>
    /// Pravilo koje je pregled prijavio kao obrnuto. Ranije: nedelja na MEV-u koja je bila
    /// laka → MEV raste. Ali „lako" je iskaz o opterećenju, a njega ispravlja progresija;
    /// minimalna doza je pitanje o stimulusu. Nedelja na MEV-u koja je **donela napredak**
    /// znači da je minimum niži nego što se mislilo.
    /// </summary>
    [Fact]
    public void Adjust_LowersMev_WhenTheMinimumVolumeStillProducedProgress()
    {
        var response = new VolumeResponse(
            PerformedSets: 9m,
            RawSets: 9m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: 0.03m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(9, result.Mev);
    }

    /// <summary>Ovoliko ne održava ni postignuto, pa je minimum viši.</summary>
    [Fact]
    public void Adjust_RaisesMev_WhenTheMinimumVolumeDidNotEvenHold()
    {
        var response = new VolumeResponse(
            PerformedSets: 9m,
            RawSets: 9m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: -0.03m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(11, result.Mev);
    }

    /// <summary>
    /// A nedelja na MEV-u koja je samo bila laka, bez ijednog podatka o snazi, ne pomera
    /// ništa. To je razlika između ćutanja i dokaza.
    /// </summary>
    [Fact]
    public void Adjust_LeavesMevAlone_WhenTheWeekWasMerelyComfortable()
    {
        var response = new VolumeResponse(PerformedSets: 9m, RawSets: 9m, AverageRirDeviation: 1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mev, result.Mev);
    }

    [Fact]
    public void Adjust_MovesAtMostOneSetPerWeek()
    {
        // Ekstreman signal ne sme da preskoči više od jednog koraka.
        var response = new VolumeResponse(PerformedSets: 22m, RawSets: 22m, AverageRirDeviation: 5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mrv + VolumeAdaptation.MaxWeeklyStep, result.Mrv);
    }

    [Fact]
    public void Adjust_StopsAtFiftyPercentAboveSeed()
    {
        // MRV 33 je tačno +50% od seed-a 22; dalje se ne ide ni posle mnogo dobrih nedelja.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 16, Mrv: 33);
        var response = new VolumeResponse(PerformedSets: 33m, RawSets: 33m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(33, result.Mrv);
    }

    [Fact]
    public void Adjust_StopsAtFiftyPercentBelowSeed()
    {
        var current = new VolumeLandmarkValues(Mev: 5, Mav: 8, Mrv: 11);
        var response = new VolumeResponse(PerformedSets: 11m, RawSets: 11m, AverageRirDeviation: -3m, FailureShare: 1m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(11, result.Mrv);
        Assert.Equal(5, result.Mev);
    }

    [Fact]
    public void Adjust_KeepsOptimalBandFromCollapsing()
    {
        // Kada bi obe granice krenule jedna ka drugoj, "optimalno" bi nestalo kao pojam.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 12);
        var response = new VolumeResponse(PerformedSets: 11m, RawSets: 11m, AverageRirDeviation: -2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.True(result.Mrv - result.Mev >= VolumeAdaptation.MinBandWidth);
    }

    [Fact]
    public void Adjust_WidensBandByRaisingMrv_RatherThanCuttingMevWithoutEvidence()
    {
        // Volumen je iznad MEV-a, pa o donjoj granici nedelja ne govori ništa.
        // Pojas se zato širi naviše; obaranje MEV-a bi bilo trajno, jer se donja
        // granica posle diže samo kada je nedelja odrađena NA njoj.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 12);
        var response = new VolumeResponse(PerformedSets: 20m, RawSets: 20m, AverageRirDeviation: -2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(10, result.Mev);
        Assert.Equal(12, result.Mrv);
    }

    [Fact]
    public void Adjust_RestoresMev_AfterHardWeeksAreFollowedByEasyOnes()
    {
        // Regresija: ranije je usko grlo obaralo MEV bez ijednog dokaza o donjoj granici,
        // pa je ostajao zaglavljen i kada se pojas ponovo otvori.
        var current = Seed;
        var hardWeek = new VolumeResponse(PerformedSets: 20m, RawSets: 20m, AverageRirDeviation: -2m, FailureShare: 0m);
        var easyWeek = new VolumeResponse(PerformedSets: 20m, RawSets: 20m, AverageRirDeviation: 2m, FailureShare: 0m);

        for (var week = 0; week < 15; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, hardWeek);
        }

        Assert.Equal(Seed.Mev, current.Mev);

        for (var week = 0; week < 10; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, easyWeek);
        }

        Assert.Equal(Seed.Mev, current.Mev);
        Assert.True(current.Mrv > 11, "MRV se posle lakih nedelja mora oporaviti sa donje ivice.");
    }

    [Fact]
    public void Adjust_NeverReturnsCollapsedBand_ForAnyReachableInput()
    {
        // CHECK ograničenje u bazi traži Mrv > Mev; ako bi algoritam ikada vratio
        // jednake vrednosti, pao bi upis usred završavanja treninga.
        var seeds = new[]
        {
            new VolumeLandmarkValues(10, 16, 22), new VolumeLandmarkValues(4, 10, 16),
            new VolumeLandmarkValues(6, 11, 16), new VolumeLandmarkValues(8, 16, 26),
            new VolumeLandmarkValues(1, 2, 3)
        };
        var responses = new[]
        {
            new VolumeResponse(0m, 0m, -3m, 1m), new VolumeResponse(1m, 1m, -3m, 1m),
            new VolumeResponse(40m, 40m, 3m, 0m), new VolumeResponse(10m, 10m, 0m, 0.25m),
            new VolumeResponse(2m, 2m, 1m, 0m)
        };

        foreach (var seed in seeds)
        {
            var current = seed;

            for (var week = 0; week < 40; week++)
            {
                foreach (var response in responses)
                {
                    current = VolumeAdaptation.Adjust(current, seed, response);

                    Assert.True(current.Mev >= 1, $"MEV {current.Mev} ispod jedinice za seed {seed}.");
                    Assert.True(
                        current.Mrv > current.Mav && current.Mav > current.Mev,
                        $"Pojas urušen ({current.Mev}/{current.Mav}/{current.Mrv}) za seed {seed}.");
                }
            }
        }
    }

    [Fact]
    public void Adjust_ConvergesInsteadOfDriftingForever()
    {
        // Dovoljno nedelja istog dobrog signala da se cap sigurno dostigne (22 -> 33
        // traži jedanaest koraka): granica raste po jednu seriju i staje na +50% od
        // seed-a umesto da beži u nedogled.
        var current = Seed;

        for (var week = 0; week < 20; week++)
        {
            var response = new VolumeResponse(current.Mrv, current.Mrv, AverageRirDeviation: 1m, FailureShare: 0m);
            current = VolumeAdaptation.Adjust(current, Seed, response);
        }

        Assert.Equal(33, current.Mrv);
    }

    [Fact]
    public void Adjust_RaisesMav_WhenTheTargetVolumeProducedNoProgress()
    {
        // Nedelja odrađena na ciljnom volumenu posle koje snaga stoji: stimulus je premali
        // za toliko rada. Ranije je isti zaključak izvlačen iz rezerve u RIR-u, što je iskaz
        // o opterećenju — i što je zajedno sa progresijom zatvaralo petlju.
        var response = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(17, result.Mav);
    }

    /// <summary>Snaga pada na volumenu oko cilja: cilj je previsok.</summary>
    [Fact]
    public void Adjust_LowersMav_WhenStrengthDeclinedAtTheTargetVolume()
    {
        var response = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: -0.03m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(15, result.Mav);
    }

    /// <summary>Napredak znači da cilj radi — tada se ne dira.</summary>
    [Fact]
    public void Adjust_HoldsMav_WhenTheTargetVolumeIsProducingProgress()
    {
        var response = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: 0.03m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mav, result.Mav);
    }

    [Fact]
    public void Adjust_LeavesMavAlone_WhenTheWeekWasFarBelowIt()
    {
        // Osam serija ne govori ništa o tome da li je cilj od šesnaest dobro postavljen.
        var response = new VolumeResponse(PerformedSets: 8m, RawSets: 8m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mav, result.Mav);
    }

    [Fact]
    public void Adjust_KeepsMavStrictlyInsideTheBand()
    {
        // MAV je cilj; van pojasa ne bi bio cilj nego još jedna granica.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 12);
        var response = new VolumeResponse(PerformedSets: 12m, RawSets: 12m, AverageRirDeviation: -3m, FailureShare: 1m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.True(result.Mev < result.Mav, $"MAV {result.Mav} nije iznad MEV {result.Mev}.");
        Assert.True(result.Mav < result.Mrv, $"MAV {result.Mav} nije ispod MRV {result.Mrv}.");
    }

    [Fact]
    public void Adjust_StopsMavAtFiftyPercentAboveSeed()
    {
        // 16 + 50% = 24; posle toga cilj prestaje da raste ma koliko nedelja prošlo.
        var current = Seed;
        var goodWeek = new VolumeResponse(
            PerformedSets: 40m,
            RawSets: 40m,
            AverageRirDeviation: 2m,
            FailureShare: 0m,
            StrengthChangeShare: 0m);

        for (var week = 0; week < 30; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, goodWeek);
        }

        Assert.Equal(24, current.Mav);
    }

    [Fact]
    public void Adjust_KeepsTheTargetAwayFromTheCeiling_AfterManyGoodWeeksNearMrv()
    {
        // Regresija: prag za pomeranje MAV-a je slabiji od praga za MRV, pa je MAV rastao
        // i na nedeljama na kojima MRV ne raste i vremenom se lepio za plafon. Ekran je
        // tada kao cilj nudio volumen jednu seriju ispod nepodnošljivog.
        var current = Seed;
        var goodWeekNearMrv = new VolumeResponse(
            PerformedSets: 20m,
            RawSets: 20m,
            AverageRirDeviation: 2m,
            FailureShare: 0m);

        for (var week = 0; week < 40; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, goodWeekNearMrv);
        }

        Assert.True(
            current.Mrv - current.Mav >= 3,
            $"Cilj {current.Mav} se slepio za plafon {current.Mrv}.");
    }

    [Fact]
    public void Adjust_StillLetsTheTargetBeLearned_RatherThanDerivedFromTheBand()
    {
        // Druga strana iste medalje: ako se cilj samo izvodi iz MEV-a i MRV-a, ne uči se
        // ništa i MAV nema smisla kao zasebna vrednost.
        var current = Seed;
        var response = new VolumeResponse(
            16m,
            16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: 0m);

        current = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.True(current.Mav > Seed.Mav, "Cilj mora da može da poraste kada nedelja to pokaže.");
    }

    /// <summary>
    /// Posledica deljene definicije signala, izmerena a ne pretpostavljena.
    ///
    /// Nedelja od deset serija: osam odrađenih tačno po planu, dve do otkaza pet
    /// ponavljanja ispod dna. Udeo otkaza je 0.2, ispod praga umora (0.25). Stara
    /// računica je otkaze puštala i u prosek RIR-a (−1.2), pa je taj isti otkaz
    /// prelazio drugi prag i MRV je padao. Sada oba signala kažu isto: dovršeni rad je
    /// išao po planu, a otkaza je bilo manje od četvrtine — pa MRV stoji.
    ///
    /// Ako je 20% otkaza dovoljno da nedelja bude „preteška", o tome se raspravlja na
    /// pragu udela otkaza, gde se to i meri. Ne kroz prosek RIR-a, koji meri drugo.
    /// </summary>
    [Fact]
    public void AWeekWithAFifthOfItsSetsFailed_NoLongerCountsAsFatiguedTwice()
    {
        RirSample[] week =
        [
            .. Enumerable.Repeat(new RirSample(new WorkingSet(10, 1), 8, 1), 8),
            .. Enumerable.Repeat(new RirSample(new WorkingSet(3, 0, IsFailure: true), 8, 1), 2)
        ];

        var shared = FatigueEvaluator.AverageRirDeviation(week);
        var overEverySet = week.Average(sample =>
            (decimal)(sample.Set.EffectiveRir(sample.RepRangeMin) - sample.TargetRir));

        Assert.Equal(0m, shared);
        Assert.Equal(-1.2m, overEverySet);

        var landmarks = new VolumeLandmarkValues(Mev: 10, Mav: 16, Mrv: 22);
        var response = new VolumeResponse(
            PerformedSets: 10,
            RawSets: 10,
            AverageRirDeviation: shared,
            FailureShare: 0.2m);

        var adjusted = VolumeAdaptation.Adjust(landmarks, landmarks, response);

        Assert.Equal(landmarks.Mrv, adjusted.Mrv);

        // Sa starom računicom bi ista nedelja spustila plafon.
        var withOldReading = VolumeAdaptation.Adjust(
            landmarks,
            landmarks,
            new VolumeResponse(10, 10, overEverySet, 0.2m));

        Assert.Equal(landmarks.Mrv - 1, withOldReading.Mrv);
    }

    /// <summary>
    /// Petlja koju je pregled prijavio, sada kao test — i merenje koje ju je pokazalo.
    ///
    /// Vežbač čije su težine prelake prijavljuje rezervu na svakoj seriji. Ranije je ista
    /// ta činjenica dizala MAV (16 → 17) **i** kroz progresiju opterećenje (100 → 107.5 kg):
    /// jedan uzrok, dve korekcije, a sa balansiranjem između njih i više serija na težem
    /// opterećenju. Sada rezervu čita samo progresija; granica volumena ćuti jer o
    /// stimulusu nije ništa rečeno.
    /// </summary>
    [Fact]
    public void AnEasyWeekAtTheTarget_NoLongerRaisesTheTargetAsWell()
    {
        var easyAtTarget = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 2m,
            FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, easyAtTarget);

        Assert.Equal(Seed.Mav, result.Mav);
        Assert.Equal(Seed.Mev, result.Mev);

        // Ista nedelja kroz progresiju i dalje diže opterećenje — ta korekcija je ostala
        // tamo gde joj je mesto.
        var progression = new ProgressionEngine().ComputeNext(
            usedWeightKg: 100m,
            workingSets: [new WorkingSet(12, 3), new WorkingSet(12, 3), new WorkingSet(12, 3)],
            targetRir: 1,
            repRangeMin: 8,
            repRangeMax: 12,
            weightStepKg: 2.5m);

        Assert.Equal(107.5m, progression.NextWeightKg);
    }

    /// <summary>
    /// Nedelja bez ijednog uporedivog merenja (prva u bloku, ili ona čija se ponavljanja ne
    /// poklapaju sa prethodnom) ne pomera ni minimum ni cilj. Plafon oporavka sme da se
    /// pomeri i tada, jer njega nose otkazi i RIR — to su iskazi o oporavku, ne o stimulusu.
    /// </summary>
    [Fact]
    public void AWeekWithoutAStrengthMeasurement_MovesNeitherTheMinimumNorTheTarget()
    {
        var silent = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: -2m,
            FailureShare: 0.3m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, silent);

        Assert.Equal(Seed.Mev, result.Mev);
        Assert.Equal(Seed.Mav, result.Mav);
        Assert.Equal(Seed.Mrv - 1, result.Mrv);
    }

    /// <summary>
    /// Pad snage je i signal umora: plafon oporavka ga čita čak i kada su RIR i otkazi
    /// uredni. Nedelja u kojoj snaga pada nije nedelja na kojoj se gradi plafon.
    /// </summary>
    [Fact]
    public void ADeclineInStrength_CountsAsFatigueForTheCeiling()
    {
        var declined = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: -0.03m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, declined);

        Assert.Equal(Seed.Mrv - 1, result.Mrv);
    }

    /// <summary>
    /// Prag razdvaja promenu od zaokruživanja: ispod jednog procenta nedelja je ravna, jer
    /// je najmanji stvaran pomak jedan korak tega (2.5 kg je 2.5% na stotinu).
    /// </summary>
    [Theory]
    [InlineData(0.005, 17)]   // ispod praga: ravna nedelja, cilj raste
    [InlineData(0.02, 16)]    // napredak: cilj stoji
    [InlineData(-0.02, 15)]   // pad: cilj se spušta
    public void TheThresholdSeparatesAChangeFromRounding(double change, int expectedMav)
    {
        var response = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: (decimal)change);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(expectedMav, result.Mav);
    }

    /// <summary>
    /// „Ravna nedelja → cilj gore" ne ide u nedogled, i koči se sama: cilj se sudi tek kada
    /// je nedelja na ≥ 90% njega, pa kako cilj raste, isti volumen prestaje da ga dodiruje.
    /// Sa nedeljom od 16 serija cilj stane na 18 — dve serije iznad polazne vrednosti.
    /// </summary>
    [Fact]
    public void RaisingTheTargetOnFlatWeeks_StopsItself()
    {
        var current = Seed;
        var flatWeek = new VolumeResponse(
            PerformedSets: 16m,
            RawSets: 16m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: 0m);

        for (var week = 0; week < 20; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, flatWeek);
        }

        Assert.Equal(18, current.Mav);
    }

    /// <summary>
    /// Uzak pojas: nedelja može istovremeno da bude na minimumu i blizu cilja, pa pad snage
    /// gura MEV gore a MAV dole. Pojas to ne sme da izvrne — cilj ostaje strogo između.
    /// </summary>
    [Fact]
    public void ANarrowBand_SurvivesAWeekThatIsBothAtTheMinimumAndNearTheTarget()
    {
        var narrow = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 13);
        var declined = new VolumeResponse(
            PerformedSets: 10m,
            RawSets: 10m,
            AverageRirDeviation: 0m,
            FailureShare: 0m,
            StrengthChangeShare: -0.03m);

        var result = VolumeAdaptation.Adjust(narrow, Seed, declined);

        Assert.True(result.Mev < result.Mav, $"MEV {result.Mev}, MAV {result.Mav}");
        Assert.True(result.Mav < result.Mrv, $"MAV {result.Mav}, MRV {result.Mrv}");
        Assert.True(result.Mrv - result.Mev >= VolumeAdaptation.MinBandWidth);
    }
}
