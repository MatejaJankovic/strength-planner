using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

public class SetLogService : ISetLogService
{
    private readonly AppDbContext _db;

    public SetLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SetLogDto> AddSetAsync(
        Guid userId,
        Guid exercisePlanId,
        AddSetLogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetInput(request.WeightKg, request.Reps, request.Rir);

        var planExists = await _db.ExercisePlans.AnyAsync(
            plan => plan.Id == exercisePlanId
                    && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId,
            cancellationToken);

        if (!planExists)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise plan was not found.");
        }

        var lastSetNumber = await _db.SetLogs
            .Where(set => set.ExercisePlanId == exercisePlanId)
            .Select(set => (int?)set.SetNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var setLog = new SetLog
        {
            Id = Guid.NewGuid(),
            ExercisePlanId = exercisePlanId,
            SetNumber = lastSetNumber + 1,
            WeightKg = request.WeightKg,
            Reps = request.Reps,
            Rir = request.Rir,
            PerformedAt = DateTime.UtcNow
        };

        _db.SetLogs.Add(setLog);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(setLog);
    }

    public async Task<SetLogDto> UpdateSetAsync(
        Guid userId,
        Guid setLogId,
        UpdateSetLogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetInput(request.WeightKg, request.Reps, request.Rir);

        var setLog = await _db.SetLogs
            .FirstOrDefaultAsync(
                set => set.Id == setLogId
                       && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (setLog is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Set log was not found.");
        }

        setLog.WeightKg = request.WeightKg;
        setLog.Reps = request.Reps;
        setLog.Rir = request.Rir;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(setLog);
    }

    public async Task DeleteSetAsync(
        Guid userId,
        Guid setLogId,
        CancellationToken cancellationToken = default)
    {
        var setLog = await _db.SetLogs
            .FirstOrDefaultAsync(
                set => set.Id == setLogId
                       && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (setLog is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Set log was not found.");
        }

        _db.SetLogs.Remove(setLog);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateSetInput(decimal weightKg, int reps, int rir)
    {
        if (weightKg < 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Weight must be greater than or equal to zero.");
        }

        if (reps <= 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Reps must be greater than zero.");
        }

        if (rir is < 0 or > 5)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "RIR must be between 0 and 5.");
        }
    }

    private static SetLogDto ToDto(SetLog setLog)
    {
        return new SetLogDto
        {
            Id = setLog.Id,
            ExercisePlanId = setLog.ExercisePlanId,
            SetNumber = setLog.SetNumber,
            WeightKg = setLog.WeightKg,
            Reps = setLog.Reps,
            Rir = setLog.Rir,
            PerformedAt = setLog.PerformedAt
        };
    }
}
