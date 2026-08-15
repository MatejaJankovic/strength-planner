using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Lični šablon: ključ kojim se prenosi kroz zahteve, i način na koji periodizacija pomera
/// brojeve koje je korisnik sam uneo.
/// </summary>
public class CustomTemplateTests
{
    [Fact]
    public void Key_RoundTripsThroughTheRequest()
    {
        var id = Guid.NewGuid();

        Assert.True(CustomTemplateKey.TryParse(CustomTemplateKey.For(id), out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void NoBuiltInKeyCanBeMistakenForACustomOne()
    {
        // Prefiks razdvaja dva izvora šablona. Ugrađen ključ sa dvotačkom bi značio da isti
        // string pokazuje na dva različita šablona, pa se to ovde zaključava.
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            Assert.DoesNotContain(":", template.Key);
            Assert.False(CustomTemplateKey.TryParse(template.Key, out _));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("upper-lower")]
    // Prefiks bez ispravnog identifikatora nije lični šablon nego neispravan ključ; poziv
    // tada mora da padne na "nepoznat šablon", a ne da traži nepostojeći red.
    [InlineData("custom:")]
    [InlineData("custom:nije-guid")]
    public void MalformedKeysAreNotTreatedAsCustomTemplates(string? templateKey)
    {
        Assert.False(CustomTemplateKey.TryParse(templateKey, out _));
    }

    [Fact]
    public void TheAthletesOwnSetsAreWhatTheDeloadHalves()
    {
        // Ovo je razlika između ličnog i ugrađenog šablona. Ugrađen šablon polazi od broja
        // serija koji nosi nivo iskustva (srednji nivo: 4), pa deload daje 2. Vežba iz
        // ličnog šablona polazi od onoga što je korisnik uneo, pa 6 serija daje 3.
        var fromExperience = Periodization.ForWeek(
            PeriodizationModel.Flat,
            weekNumber: 4,
            baseRepRangeMin: 8,
            baseRepRangeMax: 12,
            baseTargetRir: 1,
            baseSets: ExperienceProgramming.StartingSetsPerExercise(ExperienceLevel.Intermediate));

        var fromTemplate = Periodization.ForWeek(
            PeriodizationModel.Flat,
            weekNumber: 4,
            baseRepRangeMin: 8,
            baseRepRangeMax: 12,
            baseTargetRir: 1,
            baseSets: 6);

        Assert.True(fromExperience.IsDeload);
        Assert.True(fromTemplate.IsDeload);
        Assert.Equal(2, fromExperience.Sets);
        Assert.Equal(3, fromTemplate.Sets);
    }

    [Fact]
    public void PeriodizationMovesTheAthletesRangeByTheSameShapeAsTheGoalsRange()
    {
        // Nedelja 1 linearnog bloka je faza volumena: +3 ponavljanja i +1 serija. Isti
        // pomeraj pada i na propis iz cilja i na propis iz ličnog šablona, jer ForWeek i
        // inače prima "osnovu" kao parametar - zato lični šablon nije tražio svoju granu.
        var fromGoal = Periodization.ForWeek(PeriodizationModel.Linear, 1, 3, 6, 2, 4);
        var fromTemplate = Periodization.ForWeek(PeriodizationModel.Linear, 1, 5, 8, 2, 3);

        Assert.Equal(6, fromGoal.RepRangeMin);
        Assert.Equal(9, fromGoal.RepRangeMax);
        Assert.Equal(5, fromGoal.Sets);

        Assert.Equal(8, fromTemplate.RepRangeMin);
        Assert.Equal(11, fromTemplate.RepRangeMax);
        Assert.Equal(4, fromTemplate.Sets);
    }

    [Fact]
    public void RebuildingAWeekFromTheGoalWouldThrowAwayTheAthletesOwnRange()
    {
        // Zašto ExercisePlan pamti osnovni opseg umesto da ga izvodi. Kada umor povuče
        // deload ranije, oslobođena nedelja preuzima propis žrtvovane, a taj propis se
        // računa iz osnove. Dok su svi planovi delili opseg cilja, osnova se čitala sa
        // cilja i bila je tačna. Vežba iz ličnog šablona ima svoju, pa bi ista računica
        // tiho prepisala korisnikov unos.
        var athletesRange = Periodization.ForWeek(PeriodizationModel.Linear, 4, 5, 8, 1, 4);
        var goalsRange = Periodization.ForWeek(PeriodizationModel.Linear, 4, 8, 12, 1, 4);

        Assert.NotEqual(goalsRange.RepRangeMin, athletesRange.RepRangeMin);
        Assert.NotEqual(goalsRange.RepRangeMax, athletesRange.RepRangeMax);
    }

    [Fact]
    public void TheShiftCannotBeInvertedOnceTheRangeHitsItsBound()
    {
        // Drugi razlog za pamćenje osnove: za serije postoji BaseSetsFrom koji obrće
        // pomeraj, a za opseg takav postupak ne može da postoji. Periodization opseg i
        // odseca na granice, pa dve različite osnove daju istu nedelju - iz nje se ne može
        // znati od koje se pošlo.
        var fromEleven = Periodization.ForWeek(PeriodizationModel.Linear, 1, 11, 12, 1, 4);
        var fromTwelve = Periodization.ForWeek(PeriodizationModel.Linear, 1, 12, 12, 1, 4);

        Assert.Equal(fromEleven.RepRangeMin, fromTwelve.RepRangeMin);
        Assert.Equal(fromEleven.RepRangeMax, fromTwelve.RepRangeMax);
    }

    [Fact]
    public void TheFormBoundsMatchWhatAWeekCanActuallyExpress()
    {
        // Granice unosa su preuzete iz Periodization, a ne izmišljene, upravo zbog ovoga:
        // opseg iznad gornje granice bi propis nedelje tiho svukao nazad, pa bi korisnik
        // uneo 12-16 a u planu video nešto drugo. Sa granicama unosa 3-12 do toga ne dolazi.
        var clamped = Periodization.ForWeek(PeriodizationModel.Flat, 1, 12, 16, 1, 4);

        Assert.True(clamped.RepRangeMax <= Periodization.MaxReps);
        Assert.NotEqual(16, clamped.RepRangeMax);

        var withinBounds = Periodization.ForWeek(
            PeriodizationModel.Flat,
            1,
            Periodization.MinReps,
            Periodization.MaxReps,
            1,
            TrainingConstants.MaxTemplateSets);

        Assert.Equal(Periodization.MinReps, withinBounds.RepRangeMin);
        Assert.Equal(Periodization.MaxReps, withinBounds.RepRangeMax);
        Assert.Equal(TrainingConstants.MaxTemplateSets, withinBounds.Sets);
    }
}
