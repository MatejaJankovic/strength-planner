using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

public class SessionCompositionTests
{
    private sealed record Move(string Name, bool IsCompound);

    // Tipičan dan iz šablona: složene vežbe napred, izolacije iza — redosled koji
    // priručnik i inače propisuje.
    private static readonly IReadOnlyList<Move> Day =
    [
        new("Bench Press", true),
        new("Barbell Row", true),
        new("Overhead Press", true),
        new("Lat Pulldown", true),
        new("Barbell Curl", false),
        new("Triceps Pushdown", false)
    ];

    private static IReadOnlyList<string> Pick(ExperienceLevel level) =>
        SessionComposition.ForLevel(Day, move => move.IsCompound, level)
            .Select(move => move.Name)
            .ToList();

    [Fact]
    public void ForLevel_GivesABeginnerMostlyCompounds()
    {
        var chosen = Pick(ExperienceLevel.Beginner);

        Assert.Equal(
            new[] { "Bench Press", "Barbell Row", "Overhead Press", "Barbell Curl", "Triceps Pushdown" },
            chosen);
    }

    [Fact]
    public void ForLevel_GivesAnAdvancedLifterOneCompoundAndTheRestIsolation()
    {
        // Priručnik: "do 3 složene vežbe nedeljno, pretežno izolacije".
        var chosen = Pick(ExperienceLevel.Advanced);

        Assert.Equal("Bench Press", chosen[0]);
        Assert.Single(chosen.Where(name => Day.First(move => move.Name == name).IsCompound));
    }

    [Fact]
    public void ForLevel_NeverExceedsTheCompoundBudget_WhenTheDayOffersEnoughIsolation()
    {
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var compounds = SessionComposition
                .ForLevel(Day, move => move.IsCompound, level)
                .Count(move => move.IsCompound);

            Assert.True(
                compounds <= ExperienceProgramming.MaxCompoundsPerSession(level),
                $"{level}: {compounds} složenih vežbi prelazi dozvoljene "
                + $"{ExperienceProgramming.MaxCompoundsPerSession(level)}.");
        }
    }

    [Fact]
    public void ForLevel_ShortensTheSessionRatherThanBreakingTheBudget()
    {
        // Dan sa svega dve izolacije ne može naprednom vežbaču da da šest vežbi a da ne
        // prekrši pravilo; ispravno je dati kraći trening.
        var chosen = SessionComposition.ForLevel(Day, move => move.IsCompound, ExperienceLevel.Advanced);

        Assert.Equal(3, chosen.Count);
    }

    [Fact]
    public void ForLevel_KeepsCompoundsBeforeIsolations()
    {
        // Redosled je trenažno pravilo: složene vežbe se rade dok si odmoran.
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var chosen = SessionComposition.ForLevel(Day, move => move.IsCompound, level);
            var lastCompound = chosen.ToList().FindLastIndex(move => move.IsCompound);
            var firstIsolation = chosen.ToList().FindIndex(move => !move.IsCompound);

            if (lastCompound >= 0 && firstIsolation >= 0)
            {
                Assert.True(lastCompound < firstIsolation, $"{level}: izolacija je pre složene vežbe.");
            }
        }
    }

    [Fact]
    public void ForLevel_FillsTheSessionFromCompounds_WhenTheDayHasTooFewIsolations()
    {
        // Dan bez ijedne izolacije ne sme da da napredom vežbaču trening od jedne vežbe.
        IReadOnlyList<Move> compoundsOnly =
        [
            new("Back Squat", true),
            new("Deadlift", true),
            new("Leg Press", true),
            new("Front Squat", true)
        ];

        var chosen = SessionComposition.ForLevel(compoundsOnly, move => move.IsCompound, ExperienceLevel.Advanced);

        // Prag pobeđuje budžet: trening od jedne vežbe nije trening.
        Assert.Equal(SessionComposition.MinExercisesPerSession, chosen.Count);
    }

    [Fact]
    public void ForLevel_ReturnsEverythingWhenTheDayIsShorterThanTheBudget()
    {
        IReadOnlyList<Move> shortDay = [new("Bench Press", true), new("Barbell Curl", false)];

        var chosen = SessionComposition.ForLevel(shortDay, move => move.IsCompound, ExperienceLevel.Beginner);

        Assert.Equal(2, chosen.Count);
    }

    [Fact]
    public void ForLevel_HandlesAnEmptyDay()
    {
        Assert.Empty(SessionComposition.ForLevel(
            Array.Empty<Move>(),
            move => move.IsCompound,
            ExperienceLevel.Beginner));
    }
}
