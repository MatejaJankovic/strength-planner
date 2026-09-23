using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.OneRepMax;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.OneRepMax;

public class OneRepMaxService : IOneRepMaxService
{
    private readonly AppDbContext _db;

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

        return ToDto(record, exercise.Name);
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

                return chosen is null
                    // Nijedan zapis nije upotrebljiv (npr. samo stare nule sa vežbi bez
                    // opterećenja): vežba se ne prikazuje kao da ima sačuvan maksimum.
                    ? null
                    : group
                        .OrderByDescending(record => record.RecordedAt)
                        .ThenByDescending(record => record.Id)
                        .First(record => record.ValueKg == chosen.ValueKg
                                         && record.RecordedAt == chosen.RecordedAt);
            })
            .Where(record => record is not null)
            .Select(record => record!)
            .OrderBy(record => record.Exercise)
            .Select(ToDto)
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
