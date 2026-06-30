using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.OneRepMax;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
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
                record.Source.ToString(),
                record.RecordedAt))
            .ToListAsync(cancellationToken);

        return records
            .GroupBy(record => record.ExerciseId)
            .Select(group => group
                .OrderByDescending(record => record.RecordedAt)
                .ThenByDescending(record => record.Id)
                .First())
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
            Source = record.Source,
            RecordedAt = record.RecordedAt
        };
    }

    private sealed record OneRepMaxProjection(
        Guid Id,
        Guid ExerciseId,
        string Exercise,
        decimal ValueKg,
        string Source,
        DateTime RecordedAt);
}
