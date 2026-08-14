using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Svaka tabela koja nosi korisničke podatke mora da ima filter vlasništva u EF modelu.
///
/// Ovo je sloj ispod uslova po <c>userId</c> koji servisi pišu ručno: taj uslov je glavna
/// provera, a filter hvata mesto gde se izostavi. Test postoji zato što se propust ne vidi
/// — upit bez uslova radi savršeno sve dok postoji samo jedan nalog.
///
/// Model se gradi bez otvaranja veze ka bazi, pa je ovo običan unit test.
/// </summary>
public class OwnershipFilterTests
{
    private sealed class NoCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=none;Password=none")
            .Options;

        using var context = new AppDbContext(options, new NoCurrentUser());

        return context.Model;
    }

    // Nabrojano ručno, a ne izvedeno iz modela: smisao testa je da nova tabela sa
    // korisničkim podacima mora svesno da se doda i ovde, pa time i da dobije filter.
    [Theory]
    [InlineData(typeof(Profile))]
    [InlineData(typeof(Mesocycle))]
    [InlineData(typeof(Macrocycle))]
    [InlineData(typeof(MacrocycleBlock))]
    [InlineData(typeof(TrainingWeek))]
    [InlineData(typeof(WorkoutSession))]
    [InlineData(typeof(ExercisePlan))]
    [InlineData(typeof(SetLog))]
    [InlineData(typeof(OneRepMaxRecord))]
    [InlineData(typeof(UserExerciseSetting))]
    [InlineData(typeof(UserVolumeLandmark))]
    [InlineData(typeof(Exercise))]
    [InlineData(typeof(ExerciseMuscle))]
    public void EveryUserOwnedTable_HasAnOwnershipFilter(Type entityType)
    {
        var entity = BuildModel().FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.True(
            entity.GetQueryFilter() is not null,
            $"{entityType.Name} nosi korisničke podatke, a nema filter vlasništva: upit koji "
            + "zaboravi uslov po userId vratio bi tuđe redove.");
    }

    /// <summary>
    /// Šifarnici su zajednički svim nalozima. Filter na njima ne bi štitio ništa, a
    /// pokvario bi seed pri pokretanju — tada korisnika nema.
    /// </summary>
    [Theory]
    [InlineData(typeof(MuscleGroup))]
    [InlineData(typeof(VolumeLandmark))]
    public void SharedCatalogs_AreNotFiltered(Type entityType)
    {
        var entity = BuildModel().FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Null(entity.GetQueryFilter());
    }
}
