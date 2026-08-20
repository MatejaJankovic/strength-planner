using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.DTOs.Templates;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Naziv dana iz ličnog šablona mora da stane u oznaku treninga.
///
/// <c>MesocycleGenerator</c> naziv dana prepisuje u <c>WorkoutSession.DayLabel</c>, pa te
/// dve kolone nisu nezavisne — jedna je izvor druge. Bile su ipak dva broja: zahtev i
/// šablon su dopuštali 64 znaka, a oznaka treninga 32.
///
/// Posledica nije bila poruka o neispravnom unosu nego 500. Šablon sa nazivom dana od 33
/// do 64 znaka čuva se bez ijedne primedbe, a plan napravljen od njega puca na upisu —
/// dakle greška se prijavljuje na ekranu koji nema veze sa mestom gde je nastala, i to tek
/// pošto je korisnik već napravio šablon. Prijavljeno iz stvarne upotrebe.
///
/// Testira se oblik modela, bez otvaranja veze ka bazi, kao u <see cref="PlanDeletionTests"/>.
/// </summary>
public class DayLabelWidthTests
{
    private sealed class NoCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
    }

    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=none;Password=none")
            .Options;

        using var context = new AppDbContext(options, new NoCurrentUser());
        return context.Model;
    }

    private static int MaxLengthOf<TEntity>(string propertyName)
    {
        var entity = Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} nije u modelu.");

        var property = entity.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"{propertyName} nije u modelu.");

        return property.GetMaxLength()
            ?? throw new InvalidOperationException($"{propertyName} nema granicu dužine.");
    }

    [Fact]
    public void SessionDayLabelHoldsAnyTemplateDayName()
    {
        var source = MaxLengthOf<UserWorkoutTemplateDay>(nameof(UserWorkoutTemplateDay.Name));
        var derived = MaxLengthOf<WorkoutSession>(nameof(WorkoutSession.DayLabel));

        Assert.True(
            derived >= source,
            $"Naziv dana sme da ima {source} znakova, a oznaka treninga prima {derived}. "
            + "Generator naziv prepisuje u oznaku, pa se takav šablon sačuva a plan "
            + "napravljen od njega padne na upisu.");
    }

    [Fact]
    public void TheRequestDoesNotAcceptMoreThanTheColumnHolds()
    {
        // Provera zahteva i kolona moraju da čitaju istu vrednost. Da zahtev dopusti više,
        // greška bi opet stigla iz baze umesto kao poruka o neispravnom unosu.
        var day = new SaveCustomTemplateDayDto
        {
            Name = new string('a', MaxLengthOf<UserWorkoutTemplateDay>(nameof(UserWorkoutTemplateDay.Name)) + 1),
            Exercises = { new SaveCustomTemplateExerciseDto { ExerciseId = Guid.NewGuid(), Sets = 3, RepRangeMin = 8, RepRangeMax = 12 } }
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(day, new ValidationContext(day), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SaveCustomTemplateDayDto.Name)));
    }

    [Fact]
    public void AllThreeLimitsComeFromTheSameConstant()
    {
        // Tri broja koja moraju da se slažu su lakša za razlaz nego jedan koji se čita na
        // tri mesta. Ovo pada ako neko negde ponovo upiše konkretan broj.
        Assert.Equal(
            TrainingConstants.MaxDayNameLength,
            MaxLengthOf<UserWorkoutTemplateDay>(nameof(UserWorkoutTemplateDay.Name)));

        Assert.Equal(
            TrainingConstants.MaxDayNameLength,
            MaxLengthOf<WorkoutSession>(nameof(WorkoutSession.DayLabel)));
    }
}
