using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Mesocycles;

public class MesocycleService : IMesocycleService
{
    private readonly AppDbContext _db;

    public MesocycleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MesocycleSummaryDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Mesocycles
            .AsNoTracking()
            .Where(mesocycle => mesocycle.UserId == userId)
            .OrderByDescending(mesocycle => mesocycle.StartDate)
            .Select(mesocycle => new MesocycleSummaryDto
            {
                Id = mesocycle.Id,
                Name = mesocycle.Name,
                Goal = mesocycle.Goal,
                StartDate = mesocycle.StartDate,
                DurationWeeks = mesocycle.DurationWeeks,
                IsActive = mesocycle.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MesocycleDto> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var mesocycle = await BuildDetailsQuery(userId)
            .FirstOrDefaultAsync(mesocycle => mesocycle.IsActive, cancellationToken);

        if (mesocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Active mesocycle was not found.");
        }

        return ToDto(mesocycle);
    }

    public async Task<MesocycleDto> GetByIdAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default)
    {
        var mesocycle = await BuildDetailsQuery(userId)
            .FirstOrDefaultAsync(item => item.Id == mesocycleId, cancellationToken);

        if (mesocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Mesocycle was not found.");
        }

        return ToDto(mesocycle);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default)
    {
        var mesocycle = await _db.Mesocycles
            .FirstOrDefaultAsync(
                item => item.Id == mesocycleId && item.UserId == userId,
                cancellationToken);

        if (mesocycle is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Mesocycle was not found.");
        }

        _db.Mesocycles.Remove(mesocycle);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Mesocycle> BuildDetailsQuery(Guid userId)
    {
        return _db.Mesocycles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(mesocycle => mesocycle.Weeks)
                .ThenInclude(week => week.Sessions)
                    .ThenInclude(session => session.ExercisePlans)
                        .ThenInclude(plan => plan.Exercise)
            .Include(mesocycle => mesocycle.Weeks)
                .ThenInclude(week => week.Sessions)
                    .ThenInclude(session => session.ExercisePlans)
                        .ThenInclude(plan => plan.SetLogs)
            .Where(mesocycle => mesocycle.UserId == userId);
    }

    private static MesocycleDto ToDto(Mesocycle mesocycle)
    {
        return new MesocycleDto
        {
            Id = mesocycle.Id,
            Name = mesocycle.Name,
            Goal = mesocycle.Goal,
            StartDate = mesocycle.StartDate,
            DurationWeeks = mesocycle.DurationWeeks,
            IsActive = mesocycle.IsActive,
            Weeks = mesocycle.Weeks
                .OrderBy(week => week.WeekNumber)
                .Select(week => new TrainingWeekDto
                {
                    Id = week.Id,
                    WeekNumber = week.WeekNumber,
                    IsDeload = week.IsDeload,
                    Sessions = week.Sessions
                        .OrderBy(session => session.Date)
                        .ThenBy(session => session.DayLabel)
                        .Select(session => new WorkoutSessionDto
                        {
                            Id = session.Id,
                            WeekNumber = week.WeekNumber,
                            IsDeload = week.IsDeload,
                            DayLabel = session.DayLabel,
                            Date = session.Date,
                            Status = session.Status.ToString(),
                            ExercisePlans = session.ExercisePlans
                                .OrderBy(plan => plan.Order)
                                .Select(plan => new ExercisePlanDto
                                {
                                    Id = plan.Id,
                                    ExerciseId = plan.ExerciseId,
                                    ExerciseName = plan.Exercise.Name,
                                    Order = plan.Order,
                                    TargetSets = plan.TargetSets,
                                    RepRangeMin = plan.RepRangeMin,
                                    RepRangeMax = plan.RepRangeMax,
                                    TargetRir = plan.TargetRir,
                                    TargetWeightKg = plan.TargetWeightKg,
                                    SetLogs = plan.SetLogs
                                        .OrderBy(set => set.SetNumber)
                                        .Select(set => new SetLogDto
                                        {
                                            Id = set.Id,
                                            ExercisePlanId = set.ExercisePlanId,
                                            SetNumber = set.SetNumber,
                                            WeightKg = set.WeightKg,
                                            Reps = set.Reps,
                                            Rir = set.Rir,
                                            PerformedAt = set.PerformedAt
                                        })
                                        .ToList()
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
