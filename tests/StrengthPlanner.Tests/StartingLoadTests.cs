using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Od čega vežba kreće kada nijedan odrađen trening još nije progovorio o njoj.
///
/// Pravilo je izvučeno iz generatora zato što ga treba i ekran za trening: nedelja čiji
/// isti dan prethodne nedelje nije završen nema upisan cilj, pa je ekran pisao „Nema 1RM za
/// ovu vežbu" i onda kada maksimum postoji. Dva mesta, jedno pravilo.
/// </summary>
public class StartingLoadTests
{
    private static readonly E1RmCalculator Calculator = new();

    /// <summary>
    /// Isti račun koji generator radi za prvu nedelju: iz maksimuma i propisa te nedelje.
    /// Sa 1RM 140 i propisom 8 ponavljanja na RIR 1, Epley nad 9 efektivnih ponavljanja
    /// daje radnu težinu koja se zaokružuje na korak.
    /// </summary>
    [Fact]
    public void AKnownMaximum_BecomesTheLoadTheWeekPrescribes()
    {
        var load = StartingLoad.FromOneRepMax(
            Calculator,
            oneRepMaxKg: 140m,
            repRangeMin: 8,
            targetRir: 1,
            bodyweightLoadKg: 0m,
            weightStepKg: 2.5m);

        Assert.Equal(Calculator.WorkingWeightFor(140m, 8, 1, 2.5m), load);
    }

    /// <summary>Uži propis nosi veće opterećenje — isti maksimum, manje ponavljanja.</summary>
    [Fact]
    public void AHeavierPrescription_StartsHigher()
    {
        var hypertrophy = StartingLoad.FromOneRepMax(Calculator, 140m, 8, 1, 0m, 2.5m);
        var strength = StartingLoad.FromOneRepMax(Calculator, 140m, 3, 2, 0m, 2.5m);

        Assert.True(strength > hypertrophy);
    }

    /// <summary>
    /// Bez ijednog maksimuma, vežba sa spoljnim opterećenjem nema šta da kaže. To je jedini
    /// slučaj u kome ekran sme da napiše „Nema 1RM za ovu vežbu".
    /// </summary>
    [Fact]
    public void NoMaximum_AndNoBodyweight_MeansNothingIsKnown()
    {
        Assert.Null(StartingLoad.FromOneRepMax(Calculator, null, 8, 1, 0m, 2.5m));
    }

    /// <summary>
    /// Vežba koju diže telo i bez maksimuma kreće od stvarnog broja: nula dodatnih
    /// kilograma, što znači „sopstvenom masom" i tačno je. Prazno polje bi bilo netačno.
    /// </summary>
    [Fact]
    public void NoMaximum_ButBodyweight_StartsAtNothingAdded()
    {
        Assert.Equal(0m, StartingLoad.FromOneRepMax(Calculator, null, 8, 1, bodyweightLoadKg: 80m, 1m));
    }

    /// <summary>
    /// Kod vežbe sa telesnom masom se vraća ono što ide na pojas, a ne ukupno opterećenje —
    /// telo je već tu. Zaokruživanje ide u prostoru dodatih kilograma, jer korak postoji na
    /// pojasu a ne na vežbaču.
    /// </summary>
    [Fact]
    public void ABodyweightExercise_AnswersInWhatGoesOnTheBelt()
    {
        var total = Calculator.WorkingLoadFor(120m, 8, 1);
        var added = StartingLoad.FromOneRepMax(Calculator, 120m, 8, 1, bodyweightLoadKg: 80m, 1m);

        Assert.NotNull(added);
        Assert.True(added < total);
        Assert.Equal(BodyweightLoad.AddedTarget(total, 80m, 1m), added);
    }

    /// <summary>Ispod sopstvene mase nema šta da se skine, pa nema ni negativnog predloga.</summary>
    [Fact]
    public void ABodyweightExercise_NeverProposesLessThanNothing()
    {
        var added = StartingLoad.FromOneRepMax(Calculator, 60m, 12, 1, bodyweightLoadKg: 80m, 1m);

        Assert.Equal(0m, added);
    }
}
