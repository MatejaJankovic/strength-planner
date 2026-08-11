using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Mesocycles;

public class MacrocycleService : IMacrocycleService
{
    private const int BlockDurationWeeks = 4;

    private readonly AppDbContext _db;
    private readonly IMesocycleGenerator _generator;

    public MacrocycleService(AppDbContext db, IMesocycleGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    public async Task<MacrocycleDto> CreateAsync(
        Guid userId,
        CreateMacrocycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new MesocycleGenerationException("Plan name is required.");
        }

        if (!MacrocyclePlanner.IsValidBlockCount(request.Blocks.Count))
        {
            throw new MesocycleGenerationException(
                $"A plan holds between {MacrocyclePlanner.MinBlocks} and {MacrocyclePlanner.MaxBlocks} blocks.");
        }

        foreach (var block in request.Blocks)
        {
            if (WorkoutTemplateCatalog.GetByKey(block.TemplateKey) is null)
            {
                throw new MesocycleGenerationException($"Unknown workout template: '{block.TemplateKey}'.");
            }

            if (!Enum.IsDefined(block.Goal))
            {
                throw new MesocycleGenerationException($"Unsupported goal: '{block.Goal}'.");
            }
        }

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var activePlans = await _db.Macrocycles
            .Where(macrocycle => macrocycle.UserId == userId && macrocycle.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var activePlan in activePlans)
        {
            activePlan.IsActive = false;
        }

        var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
        var macrocycle = new Macrocycle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            StartDate = startDate,
            IsActive = true
        };

        for (var index = 0; index < request.Blocks.Count; index++)
        {
            var block = request.Blocks[index];
            macrocycle.Blocks.Add(new MacrocycleBlock
            {
                Id = Guid.NewGuid(),
                Order = index + 1,
                Goal = block.Goal,
                TemplateKey = block.TemplateKey
            });
        }

        _db.Macrocycles.Add(macrocycle);
        await _db.SaveChangesAsync(cancellationToken);

        // Prvi blok kreće odmah; ostali čekaju svoj red.
        var firstBlock = macrocycle.Blocks.OrderBy(block => block.Order).First();
        await GenerateForBlockAsync(userId, macrocycle, firstBlock, startDate, DateTime.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return await GetByIdAsync(userId, macrocycle.Id, cancellationToken);
    }

    public async Task<MacrocycleDto> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var macrocycle = await BuildDetailsQuery(userId)
            .FirstOrDefaultAsync(item => item.IsActive, cancellationToken);

        if (macrocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Active plan was not found.");
        }

        return await ToDtoAsync(macrocycle, cancellationToken);
    }

    public async Task<MacrocycleDto> GetByIdAsync(
        Guid userId,
        Guid macrocycleId,
        CancellationToken cancellationToken = default)
    {
        var macrocycle = await BuildDetailsQuery(userId)
            .FirstOrDefaultAsync(item => item.Id == macrocycleId, cancellationToken);

        if (macrocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Plan was not found.");
        }

        return await ToDtoAsync(macrocycle, cancellationToken);
    }

    /// <summary>
    /// Ako je mezociklus u celosti odrađen a njegov plan ima sledeći blok, generiše ga i
    /// postavlja kao aktivan. Zove se posle završetka treninga, u istoj transakciji.
    /// </summary>
    public async Task<MacrocycleAdvance?> AdvanceIfFinishedAsync(
        Guid userId,
        Guid mesocycleId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var block = await _db.MacrocycleBlocks
            .AsNoTracking()
            .Where(item => item.MesocycleId == mesocycleId && item.Macrocycle.UserId == userId)
            .Select(item => new { item.Id, item.Order, item.MacrocycleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (block is null)
        {
            return null;
        }

        var isFinished = !await _db.WorkoutSessions.AnyAsync(
            session => session.TrainingWeek.MesocycleId == mesocycleId
                       && session.Status != SessionStatus.Completed,
            cancellationToken);

        if (!isFinished)
        {
            return null;
        }

        var nextBlock = await _db.MacrocycleBlocks
            .Where(item => item.MacrocycleId == block.MacrocycleId
                           && item.Macrocycle.UserId == userId
                           && item.Order == block.Order + 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextBlock is null)
        {
            return null;
        }

        // Preuzimanje bloka pre generisanja: dva istovremena završetka poslednjeg
        // treninga ne smeju da naprave dva mezociklusa za isti blok.
        var claimed = await _db.MacrocycleBlocks
            .Where(item => item.Id == nextBlock.Id && item.GeneratedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.GeneratedAt, now),
                cancellationToken);

        if (claimed == 0)
        {
            return null;
        }

        var macrocycle = await _db.Macrocycles
            .FirstAsync(item => item.Id == block.MacrocycleId, cancellationToken);

        // Novi blok kreće od dana posle poslednjeg treninga prethodnog.
        var lastSessionDate = await _db.WorkoutSessions
            .Where(session => session.TrainingWeek.MesocycleId == mesocycleId)
            .MaxAsync(session => (DateTime?)session.Date, cancellationToken);
        var startDate = (lastSessionDate ?? now).Date.AddDays(1);

        var mesocycle = await GenerateForBlockAsync(
            userId,
            macrocycle,
            nextBlock,
            DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            now,
            cancellationToken);

        return new MacrocycleAdvance(
            macrocycle.Name,
            nextBlock.Order,
            macrocycle.Blocks.Count == 0 ? nextBlock.Order : await CountBlocksAsync(macrocycle.Id, cancellationToken),
            nextBlock.Goal,
            mesocycle.Id,
            mesocycle.Name);
    }

    private async Task<int> CountBlocksAsync(Guid macrocycleId, CancellationToken cancellationToken)
    {
        return await _db.MacrocycleBlocks.CountAsync(
            block => block.MacrocycleId == macrocycleId,
            cancellationToken);
    }

    /// <summary>
    /// Generiše mezociklus za blok i veže ga. Generator uzima najsvežije 1RM vrednosti,
    /// pa blok koji dolazi na red kreće od onoga što vežbač zaista podiže tada.
    /// </summary>
    private async Task<MesocycleDto> GenerateForBlockAsync(
        Guid userId,
        Macrocycle macrocycle,
        MacrocycleBlock block,
        DateTime startDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = WorkoutTemplateCatalog.GetByKey(block.TemplateKey)!;
        var request = new GenerateMesocycleRequest
        {
            TemplateKey = block.TemplateKey,
            Goal = block.Goal,
            Name = BuildBlockName(macrocycle.Name, block.Order, template.Name),
            StartDate = startDate
        };

        var mesocycle = await _generator.GenerateAsync(userId, request, cancellationToken);

        block.MesocycleId = mesocycle.Id;
        block.GeneratedAt ??= now;

        return mesocycle;
    }

    private static string BuildBlockName(string planName, int order, string templateName)
    {
        var name = $"{planName} — blok {order} ({templateName})";

        return name.Length <= 128 ? name : name[..128];
    }

    private IQueryable<Macrocycle> BuildDetailsQuery(Guid userId)
    {
        return _db.Macrocycles
            .AsNoTracking()
            .Include(macrocycle => macrocycle.Blocks)
            .Where(macrocycle => macrocycle.UserId == userId)
            .OrderByDescending(macrocycle => macrocycle.StartDate);
    }

    private async Task<MacrocycleDto> ToDtoAsync(Macrocycle macrocycle, CancellationToken cancellationToken)
    {
        var mesocycleIds = macrocycle.Blocks
            .Where(block => block.MesocycleId.HasValue)
            .Select(block => block.MesocycleId!.Value)
            .ToList();

        // Napredak po bloku: koliko je treninga odrađeno od ukupnog broja.
        var progress = await _db.WorkoutSessions
            .AsNoTracking()
            .Where(session => mesocycleIds.Contains(session.TrainingWeek.MesocycleId))
            .GroupBy(session => session.TrainingWeek.MesocycleId)
            .Select(group => new
            {
                MesocycleId = group.Key,
                Total = group.Count(),
                Completed = group.Count(session => session.Status == SessionStatus.Completed)
            })
            .ToDictionaryAsync(item => item.MesocycleId, cancellationToken);

        return new MacrocycleDto
        {
            Id = macrocycle.Id,
            Name = macrocycle.Name,
            StartDate = macrocycle.StartDate,
            IsActive = macrocycle.IsActive,
            Blocks = macrocycle.Blocks
                .OrderBy(block => block.Order)
                .Select(block =>
                {
                    var total = 0;
                    var completed = 0;

                    if (block.MesocycleId.HasValue
                        && progress.TryGetValue(block.MesocycleId.Value, out var counts))
                    {
                        total = counts.Total;
                        completed = counts.Completed;
                    }

                    return new MacrocycleBlockDto
                    {
                        Id = block.Id,
                        Order = block.Order,
                        Goal = block.Goal,
                        TemplateKey = block.TemplateKey,
                        TemplateName = WorkoutTemplateCatalog.GetByKey(block.TemplateKey)?.Name
                                       ?? block.TemplateKey,
                        MesocycleId = block.MesocycleId,
                        CompletedSessions = completed,
                        TotalSessions = total,
                        Status = GetStatus(block, total, completed)
                    };
                })
                .ToList()
        };
    }

    private static string GetStatus(MacrocycleBlock block, int total, int completed)
    {
        if (block.MesocycleId is null)
        {
            return "planned";
        }

        return total > 0 && completed >= total ? "completed" : "active";
    }
}
