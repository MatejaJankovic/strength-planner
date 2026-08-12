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
        var response = new VolumeResponse(PerformedSets: 21m, AverageRirDeviation: 1m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(23, result.Mrv);
        Assert.Equal(10, result.Mev);
    }

    [Fact]
    public void Adjust_DoesNotRaiseMrv_WhenWeekWasOnlyMarginallyEasierThanPrescribed()
    {
        // Ispod celog RIR poena razlika je šum procene. Prag mora da važi u oba smera,
        // inače bi se MRV penjao i na nedeljama koje su u proseku bile teže od plana.
        var response = new VolumeResponse(PerformedSets: 21m, AverageRirDeviation: 0.2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mrv, result.Mrv);
    }

    [Fact]
    public void Adjust_StillRaisesMrv_WhenOnlyTheLastSetWentToFailure()
    {
        // Poslednja serija do otkaza je uobičajena praksa; da nulti otkaz bude uslov,
        // gornja granica takvom vežbaču ne bi mogla nikada da poraste.
        var response = new VolumeResponse(PerformedSets: 21m, AverageRirDeviation: 1.5m, FailureShare: 0.05m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(23, result.Mrv);
    }

    [Fact]
    public void Adjust_LowersMrv_WhenMeaningfulVolumeProducedFatigue()
    {
        var response = new VolumeResponse(PerformedSets: 16m, AverageRirDeviation: -1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(21, result.Mrv);
    }

    [Fact]
    public void Adjust_LowersMrv_WhenAQuarterOfSetsEndedInFailure()
    {
        // Otkazi su signal umora i kada je prosečan RIR u redu.
        var response = new VolumeResponse(PerformedSets: 16m, AverageRirDeviation: 0m, FailureShare: 0.3m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(21, result.Mrv);
    }

    [Fact]
    public void Adjust_LeavesMrvAlone_WhenEasyWeekWasFarBelowTheLimit()
    {
        // Lakoća na 12 serija ne dokazuje da bi i 22 bile podnošljive.
        var response = new VolumeResponse(PerformedSets: 12m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(22, result.Mrv);
    }

    [Fact]
    public void Adjust_RaisesMev_WhenMinimumVolumeWasComfortable()
    {
        var response = new VolumeResponse(PerformedSets: 9m, AverageRirDeviation: 1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(11, result.Mev);
    }

    [Fact]
    public void Adjust_LowersMev_WhenMinimumVolumeAlreadyProducedFatigue()
    {
        var response = new VolumeResponse(PerformedSets: 9m, AverageRirDeviation: -2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(9, result.Mev);
    }

    [Fact]
    public void Adjust_MovesAtMostOneSetPerWeek()
    {
        // Ekstreman signal ne sme da preskoči više od jednog koraka.
        var response = new VolumeResponse(PerformedSets: 22m, AverageRirDeviation: 5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mrv + VolumeAdaptation.MaxWeeklyStep, result.Mrv);
    }

    [Fact]
    public void Adjust_StopsAtFiftyPercentAboveSeed()
    {
        // MRV 33 je tačno +50% od seed-a 22; dalje se ne ide ni posle mnogo dobrih nedelja.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 16, Mrv: 33);
        var response = new VolumeResponse(PerformedSets: 33m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(33, result.Mrv);
    }

    [Fact]
    public void Adjust_StopsAtFiftyPercentBelowSeed()
    {
        var current = new VolumeLandmarkValues(Mev: 5, Mav: 8, Mrv: 11);
        var response = new VolumeResponse(PerformedSets: 11m, AverageRirDeviation: -3m, FailureShare: 1m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(11, result.Mrv);
        Assert.Equal(5, result.Mev);
    }

    [Fact]
    public void Adjust_KeepsOptimalBandFromCollapsing()
    {
        // Kada bi obe granice krenule jedna ka drugoj, "optimalno" bi nestalo kao pojam.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 12);
        var response = new VolumeResponse(PerformedSets: 11m, AverageRirDeviation: -2m, FailureShare: 0m);

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
        var response = new VolumeResponse(PerformedSets: 20m, AverageRirDeviation: -2m, FailureShare: 0m);

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
        var hardWeek = new VolumeResponse(PerformedSets: 20m, AverageRirDeviation: -2m, FailureShare: 0m);
        var easyWeek = new VolumeResponse(PerformedSets: 20m, AverageRirDeviation: 2m, FailureShare: 0m);

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
            new VolumeResponse(0m, -3m, 1m), new VolumeResponse(1m, -3m, 1m),
            new VolumeResponse(40m, 3m, 0m), new VolumeResponse(10m, 0m, 0.25m),
            new VolumeResponse(2m, 1m, 0m)
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
            var response = new VolumeResponse(current.Mrv, AverageRirDeviation: 1m, FailureShare: 0m);
            current = VolumeAdaptation.Adjust(current, Seed, response);
        }

        Assert.Equal(33, current.Mrv);
    }

    [Fact]
    public void Adjust_RaisesMav_WhenTheTargetVolumeStillLeftRepsInReserve()
    {
        // Nedelja odrađena na ciljnom volumenu koja je i dalje ostavljala rezervu znači
        // da je cilj postavljen prenisko.
        var response = new VolumeResponse(PerformedSets: 16m, AverageRirDeviation: 1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(17, result.Mav);
    }

    [Fact]
    public void Adjust_LowersMav_WhenTheTargetVolumeProducedFatigue()
    {
        var response = new VolumeResponse(PerformedSets: 16m, AverageRirDeviation: -1.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(15, result.Mav);
    }

    [Fact]
    public void Adjust_LeavesMavAlone_WhenTheWeekWasFarBelowIt()
    {
        // Osam serija ne govori ništa o tome da li je cilj od šesnaest dobro postavljen.
        var response = new VolumeResponse(PerformedSets: 8m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(Seed.Mav, result.Mav);
    }

    [Fact]
    public void Adjust_KeepsMavStrictlyInsideTheBand()
    {
        // MAV je cilj; van pojasa ne bi bio cilj nego još jedna granica.
        var current = new VolumeLandmarkValues(Mev: 10, Mav: 11, Mrv: 12);
        var response = new VolumeResponse(PerformedSets: 12m, AverageRirDeviation: -3m, FailureShare: 1m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.True(result.Mev < result.Mav, $"MAV {result.Mav} nije iznad MEV {result.Mev}.");
        Assert.True(result.Mav < result.Mrv, $"MAV {result.Mav} nije ispod MRV {result.Mrv}.");
    }

    [Fact]
    public void Adjust_StopsMavAtFiftyPercentAboveSeed()
    {
        // 16 + 50% = 24; posle toga cilj prestaje da raste ma koliko nedelja prošlo.
        var current = Seed;
        var goodWeek = new VolumeResponse(PerformedSets: 40m, AverageRirDeviation: 2m, FailureShare: 0m);

        for (var week = 0; week < 30; week++)
        {
            current = VolumeAdaptation.Adjust(current, Seed, goodWeek);
        }

        Assert.Equal(24, current.Mav);
    }
}
