using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Brisanje plana mora da odnese i mezocikluse njegovih blokova.
///
/// <see cref="Infrastructure.Mesocycles.MacrocycleService.DeleteAsync"/> ih briše
/// <b>izričito</b>, i ovaj test čuva razlog zbog kog to mora: strani ključ bloka na
/// mezociklus je <c>SetNull</c>, pa kaskada plan → blokovi mezocikluse ne dodiruje. Da je
/// ikada prebačen na <c>Cascade</c>, izričito brisanje bi postalo suvišno; da je prebačen
/// na <c>Restrict</c>, brisanje plana bi pucalo.
///
/// Bez ovoga bi mezociklusi ostajali u bazi bez ijednog ekrana sa kog se vide — a to je
/// tačno onaj oblik greške koji se ne primeti, jer aplikacija posle brisanja izgleda
/// ispravno.
///
/// Model se gradi bez otvaranja veze ka bazi, pa je ovo običan unit test.
/// </summary>
public class PlanDeletionTests
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

    /// <summary>
    /// Blok nestaje sa planom. Da nije tako, brisanje plana bi ostavljalo blokove koji
    /// pokazuju na plan koga više nema.
    /// </summary>
    [Fact]
    public void DeletingAPlan_CascadesToItsBlocks()
    {
        var foreignKey = ForeignKeyFrom<MacrocycleBlock>(nameof(MacrocycleBlock.MacrocycleId));

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Mezociklus <b>ne</b> nestaje sa blokom, i to je namerno: brisanje mezociklusa ne sme
    /// da obori ceo plan. Zbog toga suprotan smer — brisanje plana — mora mezocikluse da
    /// ukloni sam, što <c>DeleteAsync</c> i radi.
    /// </summary>
    [Fact]
    public void DeletingABlock_LeavesItsMesocycleBehind()
    {
        var foreignKey = ForeignKeyFrom<MacrocycleBlock>(nameof(MacrocycleBlock.MesocycleId));

        Assert.Equal(DeleteBehavior.SetNull, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Ono što mezociklus nosi ide sa njim: nedelje, pa treninzi, pa planovi vežbi i
    /// serije. Da nedelje ne kaskadiraju, brisanje plana bi pucalo na stranom ključu
    /// umesto da prođe.
    /// </summary>
    [Theory]
    [InlineData(typeof(TrainingWeek), nameof(TrainingWeek.MesocycleId))]
    [InlineData(typeof(WorkoutSession), nameof(WorkoutSession.TrainingWeekId))]
    [InlineData(typeof(ExercisePlan), nameof(ExercisePlan.WorkoutSessionId))]
    public void DeletingAMesocycle_CascadesThroughItsContents(Type entityType, string propertyName)
    {
        var entity = Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"{entityType.Name} nije u modelu.");

        var foreignKey = entity.GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == propertyName));

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static IForeignKey ForeignKeyFrom<TEntity>(string propertyName)
    {
        var entity = Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} nije u modelu.");

        return entity.GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == propertyName));
    }
}
