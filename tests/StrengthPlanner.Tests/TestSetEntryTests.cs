using System.ComponentModel.DataAnnotations;
using System.Reflection;
using StrengthPlanner.Application.DTOs.OneRepMax;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Test-serija koju vežbač prijavi mora da prođe isti prag kao i upisana serija.
///
/// Rad obećava dva puta do početnog maksimuma: uneti poznat 1RM, ili prijaviti seriju iz
/// koje ga sistem proceni. Drugi je nedostajao. Kad se dodaje, mora da nosi i ogradu — inače
/// postaje rupa kroz koju se vraća tačno ono što je deveta runda uklonila: serija daleko od
/// otkaza koja „čita" maksimum veći nego što jeste.
/// </summary>
public class TestSetEntryTests
{
    private static RangeAttribute RangeOf(string propertyName)
    {
        var property = typeof(CreateOneRepMaxFromSetRequest).GetProperty(propertyName);
        Assert.NotNull(property);

        var range = property!.GetCustomAttribute<RangeAttribute>();
        Assert.NotNull(range);

        return range!;
    }

    /// <summary>
    /// Granice na zahtevu su ista odluka kao granice u domenu, samo napisane drugim
    /// jezikom. Ovaj test drži da se ne raziđu: dan kada se `EpleyRepCap` pomeri, a atribut
    /// ostane na 12, zahtev bi odbijao seriju koju domen prihvata — i obrnuto, što je gore.
    /// </summary>
    [Fact]
    public void TheRequestAcceptsExactlyWhatTheDomainCanEstimateFrom()
    {
        var reps = RangeOf(nameof(CreateOneRepMaxFromSetRequest.Reps));
        Assert.Equal(1, reps.Minimum);
        Assert.Equal(TrainingConstants.EpleyRepCap, reps.Maximum);

        var rir = RangeOf(nameof(CreateOneRepMaxFromSetRequest.Rir));
        Assert.Equal(0, rir.Minimum);
        Assert.Equal(TrainingConstants.E1RmMaxRir, rir.Maximum);
    }

    /// <summary>
    /// Nula kilograma je dozvoljena na zahtevu, jer kod vežbe koju diže telo opterećenje
    /// nije u polju nego na vežbaču. Da li 0 zaista prolazi odlučuje domen, nad UKUPNIM
    /// opterećenjem — i tamo vežba sa spoljnim opterećenjem pada.
    /// </summary>
    [Fact]
    public void ZeroAddedIsAllowedOnTheRequest_AndTheDomainDecidesWhetherItMeansAnything()
    {
        var weight = RangeOf(nameof(CreateOneRepMaxFromSetRequest.WeightKg));
        Assert.Equal(0d, Convert.ToDouble(weight.Minimum));

        // Bez ičega na sebi i bez telesne mase: nema šta da se skalira.
        Assert.False(E1RmCalculator.CanEstimateFrom(0m, 5, 1));

        // Zgib vežbača od 80 kg: opterećenje je tu, i procena ima smisla.
        Assert.True(E1RmCalculator.CanEstimateFrom(80m, 5, 1));
    }

    [Theory]
    [InlineData(100, 5, 1, true)]
    [InlineData(100, 12, 3, true)]
    [InlineData(100, 12, 4, false)]
    [InlineData(100, 13, 0, false)]
    [InlineData(0, 5, 0, false)]
    public void TheGuardIsTheSameOneLoggedSetsPass(double loadKg, int reps, int rir, bool expected)
    {
        Assert.Equal(expected, E1RmCalculator.CanEstimateFrom((decimal)loadKg, reps, rir));
    }

    /// <summary>
    /// Koliko ograda vredi, u brojevima: ista serija prijavljena sa rezervom od 5 „čita"
    /// znatno više nego do otkaza. Zato se iznad RIR-a 3 ne prima uopšte.
    /// </summary>
    [Fact]
    public void AFarFromFailureSetWouldHaveReadMuchHigher()
    {
        var calculator = new E1RmCalculator();

        var toFailure = calculator.EstimateOneRepMax(100m, 12, 0);
        var farFromIt = calculator.EstimateOneRepMax(100m, 12, 5);

        Assert.Equal(140m, toFailure);
        Assert.True(farFromIt > toFailure);
        Assert.False(E1RmCalculator.CanEstimateFrom(100m, 12, 5));
    }
}
