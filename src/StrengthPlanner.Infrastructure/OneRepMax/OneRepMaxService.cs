using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.OneRepMax;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.OneRepMax;

public class OneRepMaxService : IOneRepMaxService
{
    private readonly AppDbContext _db;
    private readonly E1RmCalculator _calculator = new();

    public OneRepMaxService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OneRepMaxDto> AddManualAsync(
        Guid userId,
        CreateOneRepMaxRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ValueKg <= 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "One-rep max must be greater than zero.");
        }

        var exercise = await _db.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.ExerciseId
                        && (!item.IsCustom || item.CreatedByUserId == userId),
                cancellationToken);

        if (exercise is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise was not found.");
        }

        // Vežba sa telesnom masom ne prima ručni maksimum. Njen 1RM je ukupno opterećenje
        // (telo + dodato), pa uneto „100" ne znači ništa određeno: ni 100 kg na pojasu, ni
        // 100 kg ukupno. Procena iz odrađenih serija je jedini unos koji zna svoju jedinicu.
        if (exercise.BodyweightShare > 0)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "One-rep max of a bodyweight exercise is estimated from logged sets, not entered by hand.");
        }

        var record = new OneRepMaxRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            ValueKg = request.ValueKg,
            Source = OneRepMaxSource.Manual,
            RecordedAt = DateTime.UtcNow
        };

        _db.OneRepMaxRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        await RefreshWeekOneTargetsAsync(userId, request.ExerciseId, cancellationToken);

        return ToDto(record, exercise.Name);
    }

    public async Task<OneRepMaxDto> AddFromSetAsync(
        Guid userId,
        CreateOneRepMaxFromSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exercise = await _db.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.ExerciseId
                        && (!item.IsCustom || item.CreatedByUserId == userId),
                cancellationToken);

        if (exercise is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise was not found.");
        }

        // Telo je opterećenje, pa test-serija zgiba sa 0 dodatih ima šta da kaže. Snimak
        // mase se uzima SADA, isto kao kod upisane serije.
        var bodyweightKg = exercise.BodyweightShare > 0
            ? await BodyweightPortionResolver.LoadBodyweightAsync(_db, userId, cancellationToken)
            : 0m;
        var totalLoadKg = request.WeightKg + (bodyweightKg * exercise.BodyweightShare);

        // Isti predikat koji odlučuje da li upisana serija sme da proizvede procenu. Bez
        // njega bi ovaj ekran bio rupa kroz koju se vraća tačno ono što je deveta runda
        // uklonila: 12 ponavljanja na RIR 5 „čita" 12% više nego ista serija do otkaza.
        if (!E1RmCalculator.CanEstimateFrom(totalLoadKg, request.Reps, request.Rir))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "A test set can only be estimated from when it carried load, stayed within 12 reps and ended within 3 reps of failure.");
        }

        var record = new OneRepMaxRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            ValueKg = _calculator.EstimateOneRepMax(totalLoadKg, request.Reps, request.Rir),
            // Procena, a ne ručni unos: vežbač je prijavio seriju, ne maksimum. Ručni unos
            // poništava starije procene — test-serija nema razloga da to radi, jer je i
            // sama procena iste vrste kao one iz odrađenih treninga.
            Source = OneRepMaxSource.Estimated,
            RecordedAt = DateTime.UtcNow
        };

        _db.OneRepMaxRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        await RefreshWeekOneTargetsAsync(userId, request.ExerciseId, cancellationToken);

        return ToDto(record, exercise.Name);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var exerciseId = await _db.OneRepMaxRecords
            .Where(record => record.Id == recordId && record.UserId == userId)
            .Select(record => (Guid?)record.ExerciseId)
            .FirstOrDefaultAsync(cancellationToken);

        var removed = await _db.OneRepMaxRecords
            .Where(record => record.Id == recordId && record.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed == 0 || exerciseId is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "One-rep max record was not found.");
        }

        // Obrisan zapis menja i to od kog maksimuma plan polazi, pa i opterećenja koja iz
        // njega još stoje.
        await RefreshWeekOneTargetsAsync(userId, exerciseId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<OneRepMaxDto>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .Select(record => new OneRepMaxProjection(
                record.Id,
                record.ExerciseId,
                record.Exercise.Name,
                record.ValueKg,
                record.Source,
                record.RecordedAt))
            .ToListAsync(cancellationToken);

        // Prikazuje se vrednost od koje plan zaista polazi, ne prosto najnoviji zapis.
        // Ekran „Poznati maksimumi" je do sada umeo da pokaže ručno uneto 120 kg, a
        // generator da krene od naduvane procene od 150 — dva broja za istu stvar.
        var now = DateTime.UtcNow;

        return records
            .GroupBy(record => record.ExerciseId)
            .Select(group =>
            {
                var samples = group
                    .Select(record => new OneRepMaxSample(record.ValueKg, record.Source, record.RecordedAt))
                    .ToList();
                var chosen = OneRepMaxBaseline.SelectSample(
                    samples,
                    now,
                    TrainingConstants.OneRepMaxLookbackDays,
                    allowStaleFallback: true);

                if (chosen is null)
                {
                    // Nijedan zapis nije upotrebljiv (npr. samo stare nule sa vežbi bez
                    // opterećenja): vežba se ne prikazuje kao da ima sačuvan maksimum.
                    return null;
                }

                var shown = group
                    .OrderByDescending(record => record.RecordedAt)
                    .ThenByDescending(record => record.Id)
                    .First(record => record.ValueKg == chosen.ValueKg
                                     && record.RecordedAt == chosen.RecordedAt);
                var dto = ToDto(shown);
                dto.IsOutsideLookback = OneRepMaxBaseline.IsOutsideLookback(
                    chosen,
                    now,
                    TrainingConstants.OneRepMaxLookbackDays);

                return dto;
            })
            .Where(dto => dto is not null)
            .Select(dto => dto!)
            .OrderBy(dto => dto.Exercise)
            .ToList();
    }

    public async Task<IReadOnlyList<OneRepMaxDto>> GetHistoryAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        return await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && record.ExerciseId == exerciseId)
            .OrderBy(record => record.RecordedAt)
            .ThenBy(record => record.Id)
            .Select(record => new OneRepMaxDto
            {
                Id = record.Id,
                ExerciseId = record.ExerciseId,
                Exercise = record.Exercise.Name,
                ValueKg = record.ValueKg,
                Source = record.Source.ToString(),
                RecordedAt = record.RecordedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Rewrites the stored week-one targets of this exercise that still follow the maximum
    /// (<see cref="GeneratedTarget.FollowsTheMaximum"/>), from the maximum the planner would
    /// now start from, by the same rule the generator used (<see cref="StartingLoad"/>).
    ///
    /// Without this a new maximum or test set changed the notice on the workout screen but
    /// not the load on the bar: week one is the only week whose target is written at
    /// generation, and nothing rewrote it.
    /// </summary>
    private async Task RefreshWeekOneTargetsAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken)
    {
        var candidates = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
                .ThenInclude(session => session.TrainingWeek)
            .Where(plan => plan.ExerciseId == exerciseId
                           && plan.TargetWeightKg != null
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.IsActive)
            .ToListAsync(cancellationToken);

        var plans = candidates
            .Where(plan => GeneratedTarget.FollowsTheMaximum(
                plan.WorkoutSession.TrainingWeek.WeekNumber,
                plan.WorkoutSession.Status))
            .ToList();

        if (plans.Count == 0)
        {
            return;
        }

        var samples = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && record.ExerciseId == exerciseId)
            .Select(record => new OneRepMaxSample(record.ValueKg, record.Source, record.RecordedAt))
            .ToListAsync(cancellationToken);

        // Isti izbor kao pri generisanju bloka: prazan prozor pada na najnoviji zapis ikada.
        var oneRepMax = OneRepMaxBaseline.Select(
            samples,
            DateTime.UtcNow,
            TrainingConstants.OneRepMaxLookbackDays,
            allowStaleFallback: true);

        var stepKg = WeightStepResolver.StepFor(
            await WeightStepResolver.ResolveAsync(_db, userId, [exerciseId], cancellationToken),
            exerciseId);
        var bodyweightLoadKg = BodyweightPortionResolver.PortionFor(
            await BodyweightPortionResolver.ResolveAsync(_db, userId, [exerciseId], cancellationToken),
            exerciseId);

        foreach (var plan in plans)
        {
            plan.TargetWeightKg = StartingLoad.FromOneRepMax(
                _calculator,
                oneRepMax,
                plan.RepRangeMin,
                plan.TargetRir,
                bodyweightLoadKg,
                stepKg);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static OneRepMaxDto ToDto(OneRepMaxRecord record, string exerciseName)
    {
        return new OneRepMaxDto
        {
            Id = record.Id,
            ExerciseId = record.ExerciseId,
            Exercise = exerciseName,
            ValueKg = record.ValueKg,
            Source = record.Source.ToString(),
            RecordedAt = record.RecordedAt
        };
    }

    private static OneRepMaxDto ToDto(OneRepMaxProjection record)
    {
        return new OneRepMaxDto
        {
            Id = record.Id,
            ExerciseId = record.ExerciseId,
            Exercise = record.Exercise,
            ValueKg = record.ValueKg,
            Source = record.Source.ToString(),
            RecordedAt = record.RecordedAt
        };
    }

    private sealed record OneRepMaxProjection(
        Guid Id,
        Guid ExerciseId,
        string Exercise,
        decimal ValueKg,
        OneRepMaxSource Source,
        DateTime RecordedAt);
}
