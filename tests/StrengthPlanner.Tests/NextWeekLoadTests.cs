using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Težina koju naredna nedelja dobija za istu vežbu.
///
/// Pravila su bila privatna u <c>SessionService</c> i netestirana, a jedna grana ih je
/// preskakala: vežba bez ijedne upisane serije je planiranu težinu prenosila nepromenjenu,
/// pa je deload nedelja dobijala 100% umesto 90%, a periodizovana nedelja težinu izvedenu
/// za tuđi rep-opseg.
/// </summary>
public class NextWeekLoadTests
{
    private static readonly LoadPrescription Hypertrophy = new(8, 12, 1);
    private static readonly LoadPrescription VolumeWeek = new(11, 12, 1);
    private static readonly LoadPrescription IntensityWeek = new(6, 10, 1);

    [Fact]
    public void Deload_TakesNinetyPercentOfTheLiftedLoad_NotOfTheProgression()
    {
        var next = NextWeekLoad.For(
            referenceWeightKg: 100m,
            progressionWeightKg: 102.5m,
            current: Hypertrophy,
            next: Hypertrophy,
            nextIsDeload: true,
            oneRepMaxKg: 140m,
            weightStepKg: 2.5m);

        Assert.Equal(90m, next);
    }

    [Fact]
    public void Deload_TakesNinetyPercentOfTheReference_WhenThereIsNoProgression()
    {
        // Referenca bez progresije je preskočena vežba: nosi planiranu težinu. Prijavljeno u
        // pregledu logike - ovde je stajala puna planirana težina, jer je grana bez serija
        // preskakala celo pravilo.
        var next = NextWeekLoad.For(
            referenceWeightKg: 100m,
            progressionWeightKg: null,
            current: Hypertrophy,
            next: Hypertrophy,
            nextIsDeload: true,
            oneRepMaxKg: null,
            weightStepKg: 2.5m);

        Assert.Equal(90m, next);
    }

    [Fact]
    public void Deload_DerivesFromTheOneRepMax_WhenNoLoadIsKnown()
    {
        // 100 / (1 + 9/30) = 76.92 -> 77.5 kg za propis, pa 90% = 69.75 -> 70 kg.
        var next = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: Hypertrophy,
            next: Hypertrophy,
            nextIsDeload: true,
            oneRepMaxKg: 100m,
            weightStepKg: 2.5m);

        Assert.Equal(70m, next);
    }

    [Fact]
    public void Deload_DerivesTheBaseFromTheCurrentPrescription_NotTheDeloadWeeks()
    {
        // Preskočena vežba u nedelji volumene (11-12 @RIR1) pred deload koji vraća osnovni
        // opseg (8-12 @RIR1): baza je težina za TEKUĆI propis, 100 / (1 + 12/30) = 71.43 ->
        // 72.5, pa 90% = 65.25 -> 65 kg. Da se uzimao propis deload nedelje, izašlo bi 70.
        var next = NextWeekLoad.For(
            referenceWeightKg: null,
            progressionWeightKg: null,
            current: VolumeWeek,
            next: Hypertrophy,
            nextIsDeload: true,
            oneRepMaxKg: 100m,
            weightStepKg: 2.5m);

        Assert.Equal(65m, next);
    }

    [Fact]
    public void SamePrescription_UsesTheProgression()
    {
        var next = NextWeekLoad.For(100m, 102.5m, Hypertrophy, Hypertrophy, false, 140m, 2.5m);

        Assert.Equal(102.5m, next);
    }

    [Fact]
    public void SamePrescription_CarriesTheReference_WhenThereIsNoProgression()
    {
        var next = NextWeekLoad.For(100m, null, Hypertrophy, Hypertrophy, false, null, 2.5m);

        Assert.Equal(100m, next);
    }

    [Fact]
    public void SamePrescription_DerivesFromTheOneRepMax_WhenTheWeekHasNoLoadAtAll()
    {
        var next = NextWeekLoad.For(null, null, Hypertrophy, Hypertrophy, false, 100m, 2.5m);

        Assert.Equal(77.5m, next);
    }

    [Fact]
    public void DifferentPrescription_RecomputesFromTheOneRepMax()
    {
        // 208 / (1 + 12/30) = 148.57 -> 147.5 kg za 11-12 @RIR1.
        var next = NextWeekLoad.For(160m, 162.5m, Hypertrophy, VolumeWeek, false, 208m, 2.5m);

        Assert.Equal(147.5m, next);
    }

    [Fact]
    public void DifferentPrescription_ConvertsThroughTheImpliedMax_WhenThereIsNoEstimate()
    {
        // Preskočena vežba u nedelji volumena (11-12 @RIR1) na 80 kg: implicitni maksimum je
        // 80 × (1 + 12/30) = 112 kg, a za 8-12 @RIR1 to daje 112 / 1.3 = 86.15 -> 85 kg.
        // Nošenje istih 80 kg u drugi rep-opseg je jedino što je sigurno pogrešno.
        var next = NextWeekLoad.For(80m, null, VolumeWeek, Hypertrophy, false, null, 2.5m);

        Assert.Equal(85m, next);
    }

    [Fact]
    public void DifferentPrescription_UsesTheProgressionAsTheImpliedBase_WhenLogsExist()
    {
        // Odrađena nedelja volumena: progresija je dala 82.5 kg, pa implicitni maksimum
        // 82.5 × 1.4 = 115.5 i za 6-10 @RIR1 115.5 / (1 + 7/30) = 93.65 -> 92.5 kg.
        var next = NextWeekLoad.For(80m, 82.5m, VolumeWeek, IntensityWeek, false, null, 2.5m);

        Assert.Equal(92.5m, next);
    }

    [Fact]
    public void ReturnsNull_WhenNothingAboutTheExerciseIsKnown()
    {
        Assert.Null(NextWeekLoad.For(null, null, Hypertrophy, VolumeWeek, false, null, 2.5m));
        Assert.Null(NextWeekLoad.For(null, null, Hypertrophy, Hypertrophy, true, null, 2.5m));
    }

    [Theory]
    // Deload nosi 90% prethodne težine, pa deljenje tim faktorom vraća polaznu.
    [InlineData(90.0, 2.5, 100.0)]
    [InlineData(145.0, 2.5, 160.0)]
    // Na grubom koraku deljenje nije tačan inverz: 50 / 0.9 = 55.6, a zaokruživanje na
    // najbliži korak od 10 kg dalo bi 60 - težinu koja nikada nije podignuta.
    [InlineData(50.0, 10.0, 50.0)]
    [InlineData(90.0, 10.0, 100.0)]
    [InlineData(20.0, 0.5, 22.0)]
    public void UndoDeload_RestoresTheLoadTheDeloadWasDerivedFrom(
        double deloadKg,
        double stepKg,
        double expectedKg)
    {
        Assert.Equal(
            (decimal)expectedKg,
            NextWeekLoad.UndoDeload((decimal)deloadKg, (decimal)stepKg));
    }

    [Fact]
    public void UndoDeload_IsNull_WhenTheDeloadWeekHasNoLoad()
    {
        Assert.Null(NextWeekLoad.UndoDeload(null, 2.5m));
    }

    [Theory]
    [InlineData(100.0, 102.5, 2.5)]
    [InlineData(100.0, 90.0, -10.0)]
    [InlineData(100.0, 100.0, 0.0)]
    public void ChangeKg_ComparesTheProposalWithWhatWasLifted(
        double referenceKg,
        double nextKg,
        double expectedKg)
    {
        Assert.Equal(
            (decimal)expectedKg,
            NextWeekLoad.ChangeKg((decimal)referenceKg, (decimal)nextKg));
    }

    [Fact]
    public void ChangeKg_IsNull_WhenEitherSideIsUnknown()
    {
        Assert.Null(NextWeekLoad.ChangeKg(null, 100m));
        Assert.Null(NextWeekLoad.ChangeKg(100m, null));
    }
}
