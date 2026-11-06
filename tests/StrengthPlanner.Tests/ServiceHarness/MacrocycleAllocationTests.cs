using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// „Prati moj šablon" važi samo uz lični šablon (#101). Pravilo je u domenu
/// (<c>SetAllocationRule</c>), ali ga servis primenjuje na dva mesta - kad pravi blok i kad
/// generiše njegov mezociklus - i oba su se mogla vratiti uz ceo paket zelen. Generator je
/// ovde lažan: beleži šta mu je poslato i upisuje prazan mezociklus, jer blok na njega
/// pokazuje stranim ključem.
/// </summary>
public class MacrocycleAllocationTests
{
    private static readonly string CustomKey = CustomTemplateKey.For(Guid.NewGuid());

    /// <summary>
    /// Ugrađeni blok stoji drugi, na čekanju. Prvi se generiše odmah, pa bi ga upis nazad pri
    /// generisanju ispravio i bez pravila pri pravljenju; blok na čekanju štiti samo ono.
    /// Izmereno: sa ugrađenim blokom na prvom mestu vraćanje tog mesta je ostajalo zeleno.
    /// </summary>
    [Fact]
    public async Task ABuiltInBlock_IsStoredBalanced_ACustomOneKeepsTheChoice()
    {
        using var database = new SqliteDatabase();
        await using var db = database.NewContext();
        var generator = new RecordingGenerator(db);
        var service = new MacrocycleService(db, generator, new AnyTemplate());

        var plan = await service.CreateAsync(database.UserId, new CreateMacrocycleRequest
        {
            Name = "Harness",
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            Blocks =
            [
                Block(CustomKey, SetAllocation.FollowTemplate),
                Block("upper-lower", SetAllocation.FollowTemplate)
            ]
        });

        Assert.Equal(
            [SetAllocation.FollowTemplate, SetAllocation.TargetVolume],
            plan.Blocks.OrderBy(block => block.Order).Select(block => block.SetAllocation));
        Assert.Equal(SetAllocation.FollowTemplate, generator.Requests.Single().SetAllocation);
    }

    /// <summary>
    /// Blok upisan pre pravila može da nosi „Prati moj šablon" uz ugrađeni šablon. Kad mu dođe
    /// red, generiše se balansiran, i ispravka se upisuje nazad na blok.
    /// </summary>
    [Fact]
    public async Task ABlockStoredBeforeTheRule_IsGeneratedBalanced()
    {
        using var database = new SqliteDatabase();
        Guid blockId;
        await using (var seed = database.NewContext())
        {
            var macrocycle = new Macrocycle
            {
                Id = Guid.NewGuid(),
                UserId = database.UserId,
                Name = "Stari plan",
                StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            var block = new MacrocycleBlock
            {
                Id = Guid.NewGuid(),
                Order = 1,
                Goal = Goal.Hypertrophy,
                TemplateKey = "upper-lower",
                SetAllocation = SetAllocation.FollowTemplate
            };
            macrocycle.Blocks.Add(block);
            seed.Macrocycles.Add(macrocycle);
            await seed.SaveChangesAsync();
            blockId = block.Id;
        }

        await using (var db = database.NewContext())
        {
            var generator = new RecordingGenerator(db);
            await new MacrocycleService(db, generator, new AnyTemplate()).GetActiveAsync(database.UserId);

            Assert.Equal(SetAllocation.TargetVolume, generator.Requests.Single().SetAllocation);
        }

        await using var read = database.NewContext();
        Assert.Equal(
            SetAllocation.TargetVolume,
            (await read.MacrocycleBlocks.SingleAsync(block => block.Id == blockId)).SetAllocation);
    }

    private static CreateMacrocycleBlockDto Block(string templateKey, SetAllocation allocation)
    {
        return new CreateMacrocycleBlockDto
        {
            Goal = Goal.Hypertrophy,
            TemplateKey = templateKey,
            PeriodizationModel = PeriodizationModel.Flat,
            SetAllocation = allocation
        };
    }

    private sealed class RecordingGenerator : IMesocycleGenerator
    {
        private readonly AppDbContext _db;

        public RecordingGenerator(AppDbContext db)
        {
            _db = db;
        }

        public List<GenerateMesocycleRequest> Requests { get; } = [];

        public Task<MesocycleDto> GenerateAsync(
            Guid userId,
            GenerateMesocycleRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var mesocycle = new Mesocycle
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = request.Name,
                Goal = request.Goal,
                PeriodizationModel = request.PeriodizationModel,
                SetAllocation = request.SetAllocation,
                StartDate = request.StartDate,
                IsActive = true
            };
            _db.Mesocycles.Add(mesocycle);

            return Task.FromResult(new MesocycleDto { Id = mesocycle.Id, Name = mesocycle.Name });
        }
    }

    private sealed class AnyTemplate : IWorkoutTemplateResolver
    {
        public Task<ResolvedTemplate?> ResolveAsync(
            Guid userId,
            string templateKey,
            Goal goal,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ResolvedTemplate?>(null);
        }

        public Task<string?> NameForAsync(
            Guid userId,
            string templateKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(templateKey);
        }
    }
}
