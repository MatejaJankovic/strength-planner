using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Od koje vrednosti plan polazi.
///
/// Pravilo je bilo „najbolja u poslednjih 56 dana". To važi dok su svi zapisi pouzdani, a
/// jedna optimistična RIR procena daje vrednost nekoliko procenata iznad ostalih — i onda
/// osam nedelja bude polazna težina novog bloka i osnova svakog preračuna.
/// </summary>
public class OneRepMaxBaselineTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static OneRepMaxSample Estimated(decimal valueKg, int daysAgo) =>
        new(valueKg, OneRepMaxSource.Estimated, Now.AddDays(-daysAgo));

    private static OneRepMaxSample Manual(decimal valueKg, int daysAgo) =>
        new(valueKg, OneRepMaxSource.Manual, Now.AddDays(-daysAgo));

    private static decimal? Select(params OneRepMaxSample[] samples) =>
        OneRepMaxBaseline.Select(samples, Now, TrainingConstants.OneRepMaxLookbackDays, allowStaleFallback: false);

    [Fact]
    public void Select_IgnoresALoneOutlierAboveTheTolerance()
    {
        // 150 stoji 5.9% iznad 141.7 — više od dozvoljenih 5%, pa je to jedna procena, ne
        // napredak. Tačno ovo je slika serije upisane sa preterano velikim RIR-om.
        Assert.Equal(141.7m, Select(Estimated(140m, 20), Estimated(141.7m, 10), Estimated(150m, 2)));
    }

    [Fact]
    public void Select_KeepsTheBest_WhenTheJumpIsSmallEnoughToBeProgress()
    {
        // 146 je 2.8% iznad 142: pravi napredak se kreće u manjim koracima od jedne loše
        // procene, pa ostaje najbolja vrednost.
        Assert.Equal(146m, Select(Estimated(140m, 20), Estimated(142m, 10), Estimated(146m, 2)));
    }

    [Fact]
    public void Select_ReturnsTheOnlySampleInTheWindow()
    {
        Assert.Equal(140m, Select(Estimated(140m, 3)));
    }

    [Fact]
    public void Select_LetsTheNewestManualEntrySupersedeOlderEstimates()
    {
        // Jedini način da vežbač ispravi naduvanu procenu naniže: pod pravilom "najbolja u
        // prozoru" niži ručni unos je prosto bio ignorisan.
        Assert.Equal(120m, Select(Estimated(150m, 20), Manual(120m, 5)));
    }

    [Fact]
    public void Select_CountsEstimatesRecordedAfterTheManualEntry()
    {
        // Ručni unos je izjava o danu kada je upisan, ne trajna granica.
        Assert.Equal(127m, Select(Estimated(140m, 30), Manual(120m, 20), Estimated(125m, 10), Estimated(127m, 2)));
    }

    [Fact]
    public void Select_IgnoresSamplesOlderThanTheWindow()
    {
        Assert.Equal(140m, Select(Estimated(200m, 60), Estimated(140m, 10)));
    }

    [Fact]
    public void Select_FallsBackToTheNewestSampleEver_WhenAllowed()
    {
        var samples = new[] { Estimated(200m, 400), Estimated(150m, 90) };

        Assert.Equal(
            150m,
            OneRepMaxBaseline.Select(samples, Now, TrainingConstants.OneRepMaxLookbackDays, allowStaleFallback: true));
    }

    [Fact]
    public void Select_ReturnsNull_WhenTheWindowIsEmptyAndTheFallbackIsNotAllowed()
    {
        Assert.Null(Select(Estimated(150m, 90)));
    }

    [Fact]
    public void Select_ReturnsNull_WhenThereIsNothingAtAll()
    {
        Assert.Null(Select());
    }

    [Fact]
    public void Select_IgnoresZeroValuedRecords()
    {
        // Vežbe sa telesnom masom su se upisivale sa 0 kg, pa je svaki završen trening
        // pisao procenu od 0. Takav zapis bi ovde postao ciljno opterećenje od 0 kg.
        Assert.Equal(120m, Select(Estimated(0m, 10), Estimated(120m, 1)));
        Assert.Null(Select(Estimated(0m, 10)));
        Assert.Null(OneRepMaxBaseline.Select(
            [Estimated(0m, 200)],
            Now,
            TrainingConstants.OneRepMaxLookbackDays,
            allowStaleFallback: true));
    }

    [Fact]
    public void Select_KeepsTheNewerHigherValue_WhenTheWindowHoldsOnlyTwoSamples()
    {
        // Prve dve sesije jedne vežbe: 100 × 8 do otkaza (126.67) pa 100 × 12 @RIR1
        // (143.33). Sa samo dve vrednosti "najbolja odskače od druge" znači prosto "uzmi
        // manju", a to je upravo problem zbog kog se i biralo najbolje.
        Assert.Equal(143.33m, Select(Estimated(126.67m, 7), Estimated(143.33m, 1)));
    }

    [Fact]
    public void Select_NeverDemotesAValueTheLifterTyped()
    {
        // Ručni unos od 120 kg i procena od 114.08 iz serije koja je promašila opseg:
        // 120 > 114.08 × 1.05, pa bi pravilo o ekstremu odbacilo baš ono što je vežbač
        // rekao o sebi.
        Assert.Equal(120m, Select(Manual(120m, 2), Estimated(114.08m, 1)));
        // I kad ih ima dovoljno za proveru ekstrema.
        Assert.Equal(130m, Select(Manual(130m, 3), Estimated(105m, 2), Estimated(104m, 1)));
    }

    [Fact]
    public void SelectSample_ReturnsTheRecordBehindTheChosenValue()
    {
        var manual = Manual(120m, 5);

        var chosen = OneRepMaxBaseline.SelectSample(
            [Estimated(150m, 20), manual],
            Now,
            TrainingConstants.OneRepMaxLookbackDays,
            allowStaleFallback: true);

        Assert.NotNull(chosen);
        Assert.Equal(OneRepMaxSource.Manual, chosen!.Source);
        Assert.Equal(manual.RecordedAt, chosen.RecordedAt);
    }

    [Fact]
    public void ToleranceStaysBelowTheWorstInflationTheRirFilterAllows()
    {
        // Filter propušta seriju do RIR 3: 100 kg × 12 @RIR3 čita 150, a ista serija do
        // otkaza 140 — oko 7%. Prag za ekstrem mora da bude ispod toga da bi ga uhvatio.
        var calculator = new E1RmCalculator();
        var atMaxRir = calculator.EstimateOneRepMax(100m, 12, TrainingConstants.E1RmMaxRir);
        var toFailure = calculator.EstimateOneRepMax(100m, 12);

        Assert.True(TrainingConstants.OneRepMaxOutlierTolerance < (atMaxRir / toFailure) - 1);
    }
}
