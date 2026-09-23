using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Dve kolone koje nose opterećenje sopstvenim telom, na nivou EF modela.
///
/// Test gleda model, a ne bazu, pa ne traži konekciju. Postoji zato što se obe vrednosti
/// računaju pre upisa i onda snimaju: kolona uža od izračunate vrednosti ne bi obarala
/// upis nego bi tiho zaokruživala, pa bi progresija računala sa jednim brojem a istorija
/// čuvala drugi.
/// </summary>
public class BodyweightColumnTests
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
    public void BodyweightShare_HoldsAShareWithTwoDecimals()
    {
        var property = PropertyOf<Exercise>(nameof(Exercise.BodyweightShare));

        // numeric(3,2): 0.00 do 1.00, što je tačno opseg koji udeo sme da ima.
        Assert.Equal(3, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        // Podrazumevana nula znači da vežba bez udela ostaje ono što je bila: spoljno
        // opterećenje. Migracija se zato oslanja na default, a ne na UPDATE po svakom redu.
        Assert.Equal(0m, property.GetDefaultValue());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void BodyweightShare_FitsEveryShareTheDomainAllows()
    {
        // Najveći dozvoljen udeo je celo telo. Da je kolona numeric(2,2), 1.00 se ne bi
        // moglo upisati uopšte — a upravo taj udeo nosi zgib.
        Assert.Equal(1m, BodyweightLoad.MaxShare);
        Assert.True(BodyweightLoad.IsValidShare(BodyweightLoad.MaxShare));
    }

    [Fact]
    public void BodyweightLoadKg_HoldsTheSnapshotWithTwoDecimals()
    {
        var property = PropertyOf<SetLog>(nameof(SetLog.BodyweightLoadKg));

        // numeric(6,2): do 9999.99 kg, ista skala na kojoj PortionKg zaokružuje.
        Assert.Equal(6, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal(0m, property.GetDefaultValue());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void BodyweightLoadKg_StoresExactlyWhatPortionKgProduces()
    {
        // Udeo od 0.64 na 88.01 kg daje 56.3264, što kolona sa dve decimale ne može da
        // primi. PortionKg zato zaokružuje na istu skalu; ovo vezuje to dvoje.
        var scale = PropertyOf<SetLog>(nameof(SetLog.BodyweightLoadKg)).GetScale();
        var portion = BodyweightLoad.PortionKg(88.01m, 0.64m);

        Assert.Equal(portion, Math.Round(portion, scale!.Value));
    }
}
