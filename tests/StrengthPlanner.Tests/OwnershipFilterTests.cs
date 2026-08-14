using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Svaka tabela koja nosi korisničke podatke mora da ima filter vlasništva u EF modelu,
/// i taj filter mora zaista da gleda vlasnika.
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

    /// <summary>
    /// Model se gradi jednom. EF ga kešira po tipu konteksta i po <em>instanci</em>
    /// opcija, pa bi nove opcije po testu promašile keš i svaki put ponovo prošle kroz
    /// <c>OnModelCreating</c>. Uz to se referenca ne vadi iz konteksta koji je već
    /// oslobođen — ranije je radilo samo zahvaljujući tom kešu.
    /// </summary>
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
    /// Traži da izraz filtera negde čita <c>AppDbContext.CurrentUserId</c>.
    ///
    /// Sama provera „filter postoji" ne vredi ništa: <c>HasQueryFilter(x =&gt; true)</c> je
    /// filter kao i svaki drugi i prolazi je. Upravo tim izrazom je i izmereno da bez
    /// filtera korisnik B dobija ceo tuđ mezociklus sa statusom 200 — dakle to je oblik
    /// regresije koju test mora da vidi, a ne onaj koji sme da mu promakne.
    /// </summary>
    private sealed class OwnerReferenceFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.Name == "CurrentUserId"
                && node.Member.DeclaringType == typeof(AppDbContext))
            {
                Found = true;
            }

            return base.VisitMember(node);
        }
    }

    private static bool ReadsCurrentUser(LambdaExpression filter)
    {
        var finder = new OwnerReferenceFinder();
        finder.Visit(filter);

        return finder.Found;
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
    public void EveryUserOwnedTable_IsFilteredByItsOwner(Type entityType)
    {
        var entity = Model.FindEntityType(entityType);

        Assert.NotNull(entity);

        var filter = entity.GetQueryFilter();
        Assert.True(
            filter is not null,
            $"{entityType.Name} nosi korisničke podatke, a nema filter vlasništva: upit koji "
            + "zaboravi uslov po userId vratio bi tuđe redove.");

        Assert.True(
            ReadsCurrentUser(filter!),
            $"Filter na {entityType.Name} ne čita vlasnika zahteva. Filter koji ne gleda "
            + "CurrentUserId propušta sve redove, pa je isto kao da ga nema.");
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
        var entity = Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Null(entity.GetQueryFilter());
    }

    /// <summary>
    /// Test koji čuva test: dokazuje da provera iznad zaista pada na filteru koji propušta
    /// sve. Bez ovoga bi „filter postoji" i „filter radi" izgledali isto.
    /// </summary>
    [Fact]
    public void AFilterThatIgnoresTheOwner_IsRejected()
    {
        Expression<Func<Mesocycle, bool>> passesEverything = mesocycle => true;

        Assert.False(ReadsCurrentUser(passesEverything));
    }
}
