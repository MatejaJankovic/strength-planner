using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Blok pamti nivo iskustva sa kojim je generisan.
///
/// Dve stvari se čitaju posle svake završene nedelje — skalirane granice volumena i prag
/// auto-deload-a — i obe su čitale profil. Vežbač koji promeni nivo usred bloka je time
/// prekrajao plan koji je već propisan, iako uputstvo obećava suprotno.
///
/// Model se gradi bez otvaranja veze ka bazi, pa je ovo običan unit test.
/// </summary>
public class MesocycleLevelTests
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

        var context = new AppDbContext(options, new NoCurrentUser());
        var model = context.Model;
        context.Dispose();

        return model;
    }

    private static IProperty PropertyOf<TEntity>(string name)
    {
        var entity = Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);

        var property = entity!.FindProperty(name);
        Assert.NotNull(property);

        return property!;
    }

    [Fact]
    public void Mesocycle_CarriesTheLevelItWasGeneratedWith()
    {
        var property = PropertyOf<Mesocycle>(nameof(Mesocycle.ExperienceLevel));

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(ExperienceLevel), property.ClrType);

        // Da li kolona ima podrazumevanu vrednost u bazi se odavde ne vidi:
        // GetDefaultValue() za vrednosni tip vraća njegovu nulu bez obzira na to da li je
        // default ikada konfigurisan — isto javljaju i SetAllocation i PeriodizationModel,
        // koji ga nemaju. Default koji je migracija koristila samo da doda kolonu nad
        // postojećim redovima ona sama i uklanja, pa je to tvrdnja o migraciji, ne o modelu.
    }

    [Fact]
    public void Mesocycle_AndProfile_SpeakOfTheSameLevel()
    {
        // Dve kolone, jedan pojam. Da su ikada razišle tip, generator bi upisivao jedno a
        // migracija zatečenih redova prepisivala drugo, bez ijedne greške pri prevođenju.
        Assert.Equal(
            PropertyOf<Profile>(nameof(Profile.ExperienceLevel)).ClrType,
            PropertyOf<Mesocycle>(nameof(Mesocycle.ExperienceLevel)).ClrType);
    }

    [Fact]
    public void ALevelChange_MovesEverythingTheBlockIsJudgedBy()
    {
        // Seed za grudi, kako stoji u bazi: 10 / 16 / 22.
        var chest = new VolumeLandmarkValues(10, 16, 22);

        var intermediate = ExperienceProgramming.ScaleLandmarks(chest, ExperienceLevel.Intermediate);
        var advanced = ExperienceProgramming.ScaleLandmarks(chest, ExperienceLevel.Advanced);

        // Ovo je tačno ono što je prelazak sa srednjeg na napredni nivo pomerao u bloku
        // koji je već tekao: nedeljni cilj za grudi sa 16 na 19 serija.
        Assert.Equal(16, intermediate.Mav);
        Assert.Equal(19, advanced.Mav);
        Assert.Equal(new VolumeLandmarkValues(12, 19, 26), advanced);

        // I prag iznad kog umor sam propisuje deload.
        Assert.Equal(0.60m, ExperienceProgramming.DeloadThreshold(ExperienceLevel.Intermediate));
        Assert.Equal(0.50m, ExperienceProgramming.DeloadThreshold(ExperienceLevel.Advanced));

        // Spuštanje na početnika ga uklanja u celosti — blok koji je imao auto-deload
        // prestajao bi da ga ima, usred sebe.
        Assert.Null(ExperienceProgramming.DeloadThreshold(ExperienceLevel.Beginner));
    }
}
