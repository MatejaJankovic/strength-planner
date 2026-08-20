using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests;

/// <summary>
/// Brisanje naloga mora da odnese <b>sve</b> što nalogu pripada.
///
/// Ovo je jedina nepovratna operacija u aplikaciji, i jedina kod koje se greška ne vidi:
/// posle brisanja ekrani izgledaju ispravno, a red koji je ostao u bazi nema odakle da se
/// primeti. Zato se ovde ne testira ponašanje nego <b>oblik modela</b>, isto kao u
/// <see cref="PlanDeletionTests"/> — model se gradi bez otvaranja veze ka bazi.
///
/// Dva korisnička entiteta nose samo <c>Guid</c> bez stranog ključa ka nalogu
/// (<see cref="UserWorkoutTemplate"/> i <see cref="Exercise.CreatedByUserId"/>), pa ih
/// kaskada ne dohvata i <c>DeleteAccountAsync</c> ih briše ručno. Provera ispod traži da
/// svaki takav entitet bude ili u kaskadi, ili imenovan u spisku onih koji se brišu ručno —
/// pa nov korisnički entitet ne može tiho da ostane iza.
/// </summary>
public class AccountDeletionTests
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
    /// Entiteti koje <c>DeleteAccountAsync</c> briše sam, sa razlogom zašto kaskada nije
    /// dovoljna. Dopisivanje imena ovde je svesna izjava, ne način da se test ućutka.
    /// </summary>
    private static readonly Dictionary<Type, string> DeletedByHand = new()
    {
        [typeof(UserWorkoutTemplate)] =
            "UserId je goli Guid bez stranog ključa; uz to stavke šablona drže vežbe sa Restrict.",
        [typeof(Exercise)] =
            "CreatedByUserId je goli Guid bez stranog ključa; briše se posle naloga, kada je ništa ne referiše."
    };

    /// <summary>
    /// Svojstva koja nose vlasnika, a nisu strani ključ ka nalogu.
    /// </summary>
    private static readonly string[] OwnerProperties = { "UserId", "CreatedByUserId" };

    [Fact]
    public void EveryUserOwnedEntityIsEitherCascadedOrDeletedByHand()
    {
        var missing = new List<string>();

        foreach (var entity in Model.GetEntityTypes())
        {
            var clrType = entity.ClrType;

            // Identity svoje tabele briše sam.
            if (clrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true
                || clrType == typeof(ApplicationUser))
            {
                continue;
            }

            var ownerProperty = entity.GetProperties()
                .FirstOrDefault(property => OwnerProperties.Contains(property.Name));

            if (ownerProperty is null)
            {
                continue;
            }

            // Kaskada postoji ako taj isti stub učestvuje u stranom ključu ka nalogu.
            var cascaded = entity.GetForeignKeys().Any(key =>
                key.PrincipalEntityType.ClrType == typeof(ApplicationUser)
                && key.Properties.Contains(ownerProperty)
                && key.DeleteBehavior == DeleteBehavior.Cascade);

            if (cascaded || DeletedByHand.ContainsKey(clrType))
            {
                continue;
            }

            missing.Add($"{clrType.Name}.{ownerProperty.Name}");
        }

        Assert.True(
            missing.Count == 0,
            $"Entiteti nose vlasnika a niko ih ne briše: {string.Join(", ", missing)}. "
            + "Dodaj kaskadu u ApplicationUserConfiguration, ili ih briši u "
            + "DeleteAccountAsync i upiši ih u DeletedByHand sa razlogom.");
    }

    [Fact]
    public void TheByHandListOnlyNamesEntitiesThatStillLackACascade()
    {
        // Ako neki od njih kasnije dobije kaskadu, ručno brisanje postaje suvišno i treba
        // da se ukloni - inače stoji kod koji ništa ne radi i sakriva pravo ponašanje.
        var nowCascaded = new List<string>();

        foreach (var (clrType, _) in DeletedByHand)
        {
            var entity = Model.FindEntityType(clrType);
            if (entity is null)
            {
                continue;
            }

            var cascaded = entity.GetForeignKeys().Any(key =>
                key.PrincipalEntityType.ClrType == typeof(ApplicationUser)
                && key.DeleteBehavior == DeleteBehavior.Cascade);

            if (cascaded)
            {
                nowCascaded.Add(clrType.Name);
            }
        }

        Assert.True(
            nowCascaded.Count == 0,
            $"Ovi entiteti sada imaju kaskadu ka nalogu, pa ručno brisanje u "
            + $"DeleteAccountAsync više nije potrebno: {string.Join(", ", nowCascaded)}.");
    }

    /// <summary>
    /// Red brisanja u <c>DeleteAccountAsync</c> počiva na ovim vezama. Ako bilo koja
    /// prestane da bude <c>Restrict</c>, red je slobodniji nego što kod pretpostavlja; ako
    /// se pojavi nova, red može da postane pogrešan.
    /// </summary>
    [Theory]
    [InlineData(typeof(UserWorkoutTemplateExercise), nameof(UserWorkoutTemplateExercise.ExerciseId))]
    [InlineData(typeof(ExercisePlan), nameof(ExercisePlan.ExerciseId))]
    [InlineData(typeof(OneRepMaxRecord), nameof(OneRepMaxRecord.ExerciseId))]
    public void ExerciseIsGuardedAgainstDeletionWhileReferenced(Type entityType, string propertyName)
    {
        var entity = Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"{entityType.Name} nije u modelu.");

        var foreignKey = entity.GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == propertyName));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Šablon nosi svoje dane i njihove vežbe. Da ne kaskadira, brisanje šablona — prvi
    /// korak brisanja naloga — pucalo bi na stranom ključu.
    /// </summary>
    [Theory]
    [InlineData(typeof(UserWorkoutTemplateDay), nameof(UserWorkoutTemplateDay.UserWorkoutTemplateId))]
    [InlineData(typeof(UserWorkoutTemplateExercise), nameof(UserWorkoutTemplateExercise.UserWorkoutTemplateDayId))]
    public void DeletingATemplateCascadesThroughItsContents(Type entityType, string propertyName)
    {
        var entity = Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"{entityType.Name} nije u modelu.");

        var foreignKey = entity.GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == propertyName));

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    /// <summary>Mišićne grupe vežbe idu sa vežbom, pa se ne moraju brisati ručno.</summary>
    [Fact]
    public void DeletingAnExerciseCascadesToItsMuscles()
    {
        var entity = Model.FindEntityType(typeof(ExerciseMuscle))!;

        var foreignKey = entity.GetForeignKeys()
            .Single(key => key.Properties.Any(p => p.Name == nameof(ExerciseMuscle.ExerciseId)));

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    // --- potvrda zahteva ------------------------------------------------------------

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        return results;
    }

    [Fact]
    public void DeletionRequiresBothThePasswordAndTheConfirmationWord()
    {
        // Lozinka štiti od nekoga kome je telefon ostao otključan; otkucana reč od samog
        // korisnika. Jedno bez drugog nije potvrda.
        var results = Validate(new DeleteAccountDto());

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(DeleteAccountDto.CurrentPassword)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(DeleteAccountDto.Confirmation)));
    }

    [Fact]
    public void ConfirmationWordIsTheOneTheScreenShows()
    {
        // Interfejs je na srpskom, pa je i reč koju korisnik prepisuje na srpskom. Mora da
        // prati istu vrednost u auth.models.ts.
        Assert.Equal("OBRIŠI", AccountDeletionPolicy.ConfirmationWord);
    }
}
