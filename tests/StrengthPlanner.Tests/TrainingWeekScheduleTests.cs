using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Odmor je deo plana, ne ostatak vremena — adaptacija se dešava između treninga.
/// </summary>
public class TrainingWeekScheduleTests
{
    [Theory]
    [InlineData(2, new[] { 0, 3 })]
    [InlineData(3, new[] { 0, 2, 4 })]
    [InlineData(4, new[] { 0, 1, 3, 4 })]
    [InlineData(5, new[] { 0, 1, 2, 4, 5 })]
    [InlineData(6, new[] { 0, 1, 2, 3, 4, 5 })]
    public void OffsetFor_SpreadsTheWeekAsPlanned(int daysPerWeek, int[] expected)
    {
        var actual = Enumerable
            .Range(0, daysPerWeek)
            .Select(dayIndex => TrainingWeekSchedule.OffsetFor(daysPerWeek, dayIndex))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OffsetFor_KeepsThePreviousSpacingForThreeAndFourDayTemplates()
    {
        // Zatečeni šabloni ne smeju da promene raspored zbog ove izmene.
        Assert.Equal(new[] { 0, 2, 4 }, Offsets(3));
        Assert.Equal(new[] { 0, 1, 3, 4 }, Offsets(4));
    }

    [Fact]
    public void OffsetFor_NeverPutsTwoSessionsOnTheSameDay()
    {
        for (var daysPerWeek = 1; daysPerWeek <= TrainingWeekSchedule.MaxDaysPerWeek; daysPerWeek++)
        {
            var offsets = Offsets(daysPerWeek);

            Assert.Equal(offsets.Length, offsets.Distinct().Count());
        }
    }

    [Fact]
    public void OffsetFor_KeepsEveryTemplateInsideOneWeek()
    {
        // Pomeraj od sedam ili više gurnuo bi trening u sledeću nedelju plana.
        for (var daysPerWeek = 1; daysPerWeek <= TrainingWeekSchedule.MaxDaysPerWeek; daysPerWeek++)
        {
            Assert.All(Offsets(daysPerWeek), offset => Assert.InRange(offset, 0, 6));
        }
    }

    [Fact]
    public void OffsetFor_KeepsTheDaysInOrder()
    {
        for (var daysPerWeek = 1; daysPerWeek <= TrainingWeekSchedule.MaxDaysPerWeek; daysPerWeek++)
        {
            var offsets = Offsets(daysPerWeek);

            Assert.Equal(offsets.OrderBy(offset => offset), offsets);
        }
    }

    [Fact]
    public void OffsetFor_FallsBackToConsecutiveDaysForWeekShapesItDoesNotKnow()
    {
        // Šablon duži od nedelje nije predviđen, ali plan koji se ipak rasporedi je
        // bolji od izuzetka usred generisanja.
        Assert.Equal(9, TrainingWeekSchedule.OffsetFor(10, 9));
        Assert.Equal(0, TrainingWeekSchedule.OffsetFor(10, 0));
    }

    [Fact]
    public void OffsetFor_RejectsADayIndexOutsideTheWeek()
    {
        // Za trening koji nedelja ne sadrži nema ispravnog dana. Vraćanje samog indeksa
        // bi dva treninga smestilo na isti datum: četiri dana daju pomeraje 0, 1, 3, 4,
        // pa bi i indeks 4 pao na 4.
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingWeekSchedule.OffsetFor(3, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingWeekSchedule.OffsetFor(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingWeekSchedule.OffsetFor(4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingWeekSchedule.OffsetFor(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingWeekSchedule.OffsetFor(-1, 0));
    }

    [Fact]
    public void OffsetFor_NeverPutsTwoSessionsOnTheSameDay_ForEveryWeekShapeItAccepts()
    {
        // Prethodna verzija je za indeks van nedelje vraćala sam indeks, pa su se
        // pomeraji poklapali. Ovde se prolazi i kroz oblike nedelje koje tabela ne
        // pokriva, do granice na kojoj poziv postaje greška.
        for (var daysPerWeek = 1; daysPerWeek <= 14; daysPerWeek++)
        {
            var offsets = Enumerable
                .Range(0, daysPerWeek)
                .Select(dayIndex => TrainingWeekSchedule.OffsetFor(daysPerWeek, dayIndex))
                .ToList();

            Assert.Equal(offsets.Count, offsets.Distinct().Count());
        }
    }

    private static int[] Offsets(int daysPerWeek) =>
        Enumerable
            .Range(0, daysPerWeek)
            .Select(dayIndex => TrainingWeekSchedule.OffsetFor(daysPerWeek, dayIndex))
            .ToArray();
}
