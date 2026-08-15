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

            // Nepoznat model bi se tiho ponašao kao ravan, ali bi se upisao u bazu i
            // vraćao klijentu kao vrednost koju nijedan ekran ne ume da prikaže.
            if (!Enum.IsDefined(block.PeriodizationModel))
            {
                throw new MesocycleGenerationException(
                    $"Unsupported periodization model: '{block.PeriodizationModel}'.");
            }
        }

        // await using i na null-u je legalan no-op, pa se transakcija oslobadja i kada
        // se izadje izuzetkom, a ne samo posle uspesnog commit-a.
        await using var transaction = _db.Database.CurrentTransaction is null
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
                TemplateKey = block.TemplateKey,
                PeriodizationModel = block.PeriodizationModel
            });
        }

        _db.Macrocycles.Add(macrocycle);
        await _db.SaveChangesAsync(cancellationToken);

        // Prvi blok kreće odmah; ostali čekaju svoj red.
        var firstBlock = macrocycle.Blocks.OrderBy(block => block.Order).First();
        await GenerateForBlockAsync(
            userId,
            macrocycle,
            firstBlock,
            macrocycle.Blocks.Count,
            startDate,
            DateTime.UtcNow,
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetByIdAsync(userId, macrocycle.Id, cancellationToken);
    }

    public IReadOnlyList<CreateMacrocycleBlockDto> SuggestBlocks(
        int blockCount,
        Goal firstGoal,
        string templateKey)
    {
        if (!MacrocyclePlanner.IsValidBlockCount(blockCount))
        {
            throw new MesocycleGenerationException(
                $"A plan holds between {MacrocyclePlanner.MinBlocks} and {MacrocyclePlanner.MaxBlocks} blocks.");
        }

        if (WorkoutTemplateCatalog.GetByKey(templateKey) is null)
        {
            throw new MesocycleGenerationException($"Unknown workout template: '{templateKey}'.");
        }

        return MacrocyclePlanner
            .AlternatingGoals(blockCount, firstGoal)
            // Prvi blok gradi volumen, naredni ga pretvara u snagu — pa se i model
            // periodizacije smenjuje zajedno sa ciljem.
            .Select(goal => new CreateMacrocycleBlockDto
            {
                Goal = goal,
                TemplateKey = templateKey,
                PeriodizationModel = goal == Goal.Strength
                    ? PeriodizationModel.Linear
                    : PeriodizationModel.Inverse
            })
            .ToList();
    }

    public async Task<MacrocycleDto> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCurrentBlockAsync(userId, cancellationToken);

        var macrocycle = await BuildDetailsQuery(userId)
            .FirstOrDefaultAsync(item => item.IsActive, cancellationToken);

        if (macrocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Active plan was not found.");
        }

        return await ToDtoAsync(macrocycle, cancellationToken);
    }

    /// <summary>
    /// Popravlja plan koji je ostao bez tekućeg bloka. Prelazak inače pokreće završetak
    /// poslednjeg treninga, ali taj okidač može da promaši: mezociklus bude obrisan, ili
    /// dva istovremena završetka poslednje dve sesije se međusobno ne vide, pa nijedan
    /// nedelju ne prepozna kao gotovu. Bez ovoga plan tu ostaje zauvek zaglavljen.
    /// Preuzimanje ide istim uslovnim UPDATE-om, pa je bezbedno pozvati sa čitanja.
    /// </summary>
    private async Task EnsureCurrentBlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await _db.Macrocycles
            .AsNoTracking()
            .Where(macrocycle => macrocycle.UserId == userId && macrocycle.IsActive)
            .Select(macrocycle => new { macrocycle.Id, macrocycle.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            return;
        }

        var blocks = await _db.MacrocycleBlocks
            .AsNoTracking()
            .Where(block => block.MacrocycleId == plan.Id && block.Macrocycle.UserId == userId)
            .OrderBy(block => block.Order)
            .Select(block => new { block.Id, block.Order, block.MesocycleId, block.GeneratedAt })
            .ToListAsync(cancellationToken);

        if (blocks.Count == 0)
        {
            return;
        }

        var live = blocks.Where(block => block.MesocycleId.HasValue).ToList();

        // Nijedan blok nema mezociklus: ili prvi nikada nije napravljen, ili je obrisan.
        if (live.Count == 0)
        {
            await RegenerateBlockAsync(userId, plan.Id, blocks[0].Id, cancellationToken);
            return;
        }

        var lastLive = live[^1];
        var isFinished = !await _db.WorkoutSessions.AnyAsync(
            session => session.TrainingWeek.MesocycleId == lastLive.MesocycleId!.Value
                       && session.TrainingWeek.Mesocycle.UserId == userId
                       && session.Status != SessionStatus.Completed,
            cancellationToken);

        if (!isFinished)
        {
            return;
        }

        var next = blocks.FirstOrDefault(block => block.Order == lastLive.Order + 1);
        if (next is not null && next.MesocycleId is null)
        {
            await RegenerateBlockAsync(userId, plan.Id, next.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Generiše mezociklus za blok koji ga nema, bez obzira na to da li je ranije već
    /// bio preuzet (obrisan mezociklus ostavlja GeneratedAt iza sebe).
    /// </summary>
    private async Task RegenerateBlockAsync(
        Guid userId,
        Guid macrocycleId,
        Guid blockId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var claimed = await _db.MacrocycleBlocks
            .Where(block => block.Id == blockId
                            && block.Macrocycle.UserId == userId
                            && block.MesocycleId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(block => block.GeneratedAt, now),
                cancellationToken);

        if (claimed == 0)
        {
            return;
        }

        var macrocycle = await _db.Macrocycles
            .FirstAsync(item => item.Id == macrocycleId && item.UserId == userId, cancellationToken);
        var block = await _db.MacrocycleBlocks.FirstAsync(item => item.Id == blockId, cancellationToken);
        var blockCount = await CountBlocksAsync(userId, macrocycleId, cancellationToken);

        try
        {
            await GenerateForBlockAsync(
                userId,
                macrocycle,
                block,
                blockCount,
                DateTime.SpecifyKind(now.Date, DateTimeKind.Utc),
                now,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (MesocycleGenerationException)
        {
            // Šablon više nije generisiv (obrisana vežba). Plan ostaje kakav jeste;
            // čitanje ne sme da pukne zbog toga.
            _db.ChangeTracker.Clear();
        }
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
            // Samo aktivan plan sme da napreduje. Bez ovoga bi dovrsavanje zaostalog
            // treninga iz napustenog plana generisalo njegov sledeci blok i preotelo
            // aktivni mezociklus planu koji korisnik zaista prati.
            .Where(item => item.MesocycleId == mesocycleId
                           && item.Macrocycle.UserId == userId
                           && item.Macrocycle.IsActive)
            .Select(item => new { item.Id, item.Order, item.MacrocycleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (block is null)
        {
            return null;
        }

        var isFinished = !await _db.WorkoutSessions.AnyAsync(
            session => session.TrainingWeek.MesocycleId == mesocycleId
                       && session.TrainingWeek.Mesocycle.UserId == userId
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
            .FirstAsync(
                item => item.Id == block.MacrocycleId && item.UserId == userId,
                cancellationToken);

        // Novi blok kreće od dana posle poslednjeg treninga prethodnog.
        var lastSessionDate = await _db.WorkoutSessions
            .Where(session => session.TrainingWeek.MesocycleId == mesocycleId
                              && session.TrainingWeek.Mesocycle.UserId == userId)
            .MaxAsync(session => (DateTime?)session.Date, cancellationToken);

        // Blok krece dan posle prethodnog, ali nikad u proslosti: ko je cetvoronedeljni
        // blok razvukao na sedam nedelja ne sme da dobije plan koji je vec zakasnio.
        var previousEnd = (lastSessionDate ?? now).Date.AddDays(1);
        var startDate = previousEnd > now.Date ? previousEnd : now.Date;

        var blockCount = await CountBlocksAsync(userId, macrocycle.Id, cancellationToken);
        var mesocycle = await GenerateForBlockAsync(
            userId,
            macrocycle,
            nextBlock,
            blockCount,
            DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            now,
            cancellationToken);

        return new MacrocycleAdvance(
            macrocycle.Name,
            nextBlock.Order,
            blockCount,
            nextBlock.Goal,
            mesocycle.Id,
            mesocycle.Name);
    }

    private async Task<int> CountBlocksAsync(
        Guid userId,
        Guid macrocycleId,
        CancellationToken cancellationToken)
    {
        return await _db.MacrocycleBlocks.CountAsync(
            block => block.MacrocycleId == macrocycleId && block.Macrocycle.UserId == userId,
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
        int blockCount,
        DateTime startDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = WorkoutTemplateCatalog.GetByKey(block.TemplateKey)!;
        var request = new GenerateMesocycleRequest
        {
            TemplateKey = block.TemplateKey,
            Goal = block.Goal,
            PeriodizationModel = block.PeriodizationModel,
            Name = BuildBlockName(macrocycle.Name, block.Order, blockCount, template.Name),
            StartDate = startDate
        };

        var mesocycle = await _generator.GenerateAsync(userId, request, cancellationToken);

        block.MesocycleId = mesocycle.Id;
        block.GeneratedAt ??= now;

        return mesocycle;
    }

    /// <summary>
    /// Plan sa jednim blokom zadrzava naziv koji je korisnik uneo — iz njegovog ugla je
    /// to i dalje obican novi mezociklus. Tek kod vise blokova ima smisla razlikovati ih.
    /// </summary>
    private static string BuildBlockName(string planName, int order, int blockCount, string templateName)
    {
        if (blockCount <= 1)
        {
            return planName;
        }

        var name = $"{planName} - blok {order} ({templateName})";

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
            .Where(session => mesocycleIds.Contains(session.TrainingWeek.MesocycleId)
                              && session.TrainingWeek.Mesocycle.UserId == macrocycle.UserId)
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
                        PeriodizationModel = block.PeriodizationModel,
                        // Trajanje se izvodi iz modela, pa plan zna svoju dužinu i pre
                        // nego što je blok uopšte generisan.
                        DurationWeeks = Periodization.DurationWeeks(block.PeriodizationModel),
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
            // GeneratedAt bez mezociklusa znaci da je mezociklus obrisan: blok je bio
            // pokrenut pa ponisten, a ne da tek ceka svoj red. Bez te razlike bi ekran
            // obecavao generisanje koje nikada nece doci.
            return block.GeneratedAt is null ? "planned" : "cancelled";
        }

        return total > 0 && completed >= total ? "completed" : "active";
    }
}
