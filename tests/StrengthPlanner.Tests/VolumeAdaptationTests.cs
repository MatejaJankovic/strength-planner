using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Seed MEV/MRV vrednosti su populacioni prosek; ovi testovi pokrivaju kako se pomeraju
/// ka onome što konkretan korisnik stvarno podnosi.
/// </summary>
public class VolumeAdaptationTests
{
    // Chest iz seed-a: MEV 10, MRV 22.
    private static readonly VolumeLandmarkValues Seed = new(Mev: 10, Mrv: 22);

    [Fact]
    public void Adjust_RaisesMrv_WhenNearMaxVolumeStillLeftRepsInReserve()
    {
        // 21 serija je iznad 90% MRV-a, a prosečno odstupanje RIR-a je pozitivno:
        // korisnik podnosi više nego što populaciona granica pretpostavlja.
        var response = new VolumeResponse(PerformedSets: 21m, AverageRirDeviation: 0.5m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(Seed, Seed, response);

        Assert.Equal(23, result.Mrv);
        Assert.Equal(10, result.Mev);
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
        var current = new VolumeLandmarkValues(Mev: 10, Mrv: 33);
        var response = new VolumeResponse(PerformedSets: 33m, AverageRirDeviation: 2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(33, result.Mrv);
    }

    [Fact]
    public void Adjust_StopsAtFiftyPercentBelowSeed()
    {
        var current = new VolumeLandmarkValues(Mev: 5, Mrv: 11);
        var response = new VolumeResponse(PerformedSets: 11m, AverageRirDeviation: -3m, FailureShare: 1m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.Equal(11, result.Mrv);
        Assert.Equal(5, result.Mev);
    }

    [Fact]
    public void Adjust_KeepsOptimalBandFromCollapsing()
    {
        // Kada bi obe granice krenule jedna ka drugoj, "optimalno" bi nestalo kao pojam.
        var current = new VolumeLandmarkValues(Mev: 10, Mrv: 11);
        var response = new VolumeResponse(PerformedSets: 11m, AverageRirDeviation: -2m, FailureShare: 0m);

        var result = VolumeAdaptation.Adjust(current, Seed, response);

        Assert.True(result.Mrv - result.Mev >= VolumeAdaptation.MinBandWidth);
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
}
