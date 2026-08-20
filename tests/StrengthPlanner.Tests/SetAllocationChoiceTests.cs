using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Ko odlučuje o broju serija — planer ili korisnikov šablon.
///
/// Od runde 5 planer bira serije tako da nedelja padne u ciljnu zonu volumena po mišiću.
/// Za ugrađene šablone je to ispravno: oni nose raspored vežbi, ne nameru o volumenu. Za
/// lični šablon nije: prijavljeno iz stvarne upotrebe — uneto 3 serije, plan propisao 5, i
/// nigde nije pisalo zašto.
///
/// Izbor je sada po bloku. Testovi ispod drže tri stvari koje taj izbor čine upotrebljivim:
/// da zatečeni planovi ostaju kakvi jesu, da izbor stigne do mezociklusa (jer se serije
/// balansiraju i posle svakog završenog treninga, kada se blok više ne čita), i da
/// neispravna vrednost u zahtevu bude odbijena.
/// </summary>
public class SetAllocationChoiceTests
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

    /// <summary>
    /// Zatečeno ponašanje mora da bude nulta vrednost enuma.
    ///
    /// Migracija dodaje kolonu sa podrazumevanom nulom, pa svaki plan napravljen pre ovog
    /// izbora nastavlja da cilja volumen. Da je <c>FollowTemplate</c> nula, svi zatečeni
    /// blokovi bi tiho prestali da se balansiraju.
    /// </summary>
    [Fact]
    public void TargetVolumeIsTheZeroValue()
    {
        Assert.Equal(0, (int)SetAllocation.TargetVolume);
    }

    [Fact]
    public void NewPlansDefaultToTargetVolume()
    {
        Assert.Equal(SetAllocation.TargetVolume, new MacrocycleBlock().SetAllocation);
        Assert.Equal(SetAllocation.TargetVolume, new Mesocycle().SetAllocation);
        Assert.Equal(SetAllocation.TargetVolume, new CreateMacrocycleBlockDto().SetAllocation);
    }

    /// <summary>
    /// Izbor mora da stoji i na mezociklusu, ne samo na bloku.
    ///
    /// Serije se ponovo balansiraju posle svakog završenog treninga
    /// (<c>SessionService.CompleteAsync</c>), a tamo se blok ne čita — pita se mezociklus.
    /// Da izbor živi samo na bloku, važio bi tačno do prvog odrađenog treninga.
    /// </summary>
    [Theory]
    [InlineData(typeof(MacrocycleBlock))]
    [InlineData(typeof(Mesocycle))]
    public void TheChoiceIsPersistedOnBothEntities(Type entityType)
    {
        var entity = Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"{entityType.Name} nije u modelu.");

        var property = entity.FindProperty(nameof(MacrocycleBlock.SetAllocation));

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    /// <summary>
    /// Blok se generiše tek kada dođe na red — mesecima posle pravljenja plana. Izbor zato
    /// mora da preživi u bazi, a ne da se čita iz zahteva koji ga je napravio.
    /// </summary>
    [Fact]
    public void TheGenerateRequestCarriesEveryChoiceTheBlockStores()
    {
        var blockChoices = typeof(MacrocycleBlock)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsEnum)
            .Select(property => property.Name)
            .ToList();

        var requestChoices = typeof(Application.DTOs.Mesocycles.GenerateMesocycleRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet();

        var missing = blockChoices.Where(name => !requestChoices.Contains(name)).ToList();

        Assert.True(
            missing.Count == 0,
            $"Blok pamti izbore koje zahtev za generisanje ne nosi: {string.Join(", ", missing)}. "
            + "Blok se generiše kada dođe na red, pa bi takav izbor tiho pao na podrazumevanu "
            + "vrednost umesto na ono što je korisnik izabrao.");
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public void AnUndefinedChoiceIsRejected(int value)
    {
        // Model binder prima bilo koji ceo broj za enum. Bez provere bi se u plan upisala
        // vrednost koju nijedna grana koda ne prepoznaje, pa bi pala na "cilja volumen"
        // iako korisnik to nije tražio.
        var dto = new CreateMacrocycleBlockDto
        {
            TemplateKey = "full-body",
            SetAllocation = (SetAllocation)value
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(CreateMacrocycleBlockDto.SetAllocation)));
    }
}
