using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Analytics;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

public class VolumeService : IVolumeService
{
    private readonly AppDbContext _db;

    public VolumeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WeeklyVolumeDto>> GetWeeklyVolumeAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        CancellationToken cancellationToken = default)
    {
        if (weekNumber <= 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Week number must be greater than zero.");
        }

        var weekExists = await _db.TrainingWeeks.AnyAsync(
            week => week.MesocycleId == mesocycleId
                    && week.WeekNumber == weekNumber
                    && week.Mesocycle.UserId == userId,
            cancellationToken);

        if (!weekExists)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Training week was not found.");
        }

        var volumeByMuscleGroupId = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == weekNumber
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .SelectMany(set => set.ExercisePlan.Exercise.Muscles
                .Select(muscle => new
                {
                    muscle.MuscleGroupId,
                    muscle.Contribution
                }))
            .GroupBy(muscle => muscle.MuscleGroupId)
            .Select(group => new
            {
                MuscleGroupId = group.Key,
                Sets = group.Sum(item => item.Contribution)
            })
            .ToDictionaryAsync(item => item.MuscleGroupId, item => item.Sets, cancellationToken);

        var landmarks = await _db.VolumeLandmarks
            .AsNoTracking()
            .Include(landmark => landmark.MuscleGroup)
            .OrderBy(landmark => landmark.MuscleGroup.Name)
            .Select(landmark => new
            {
                landmark.MuscleGroupId,
                Muscle = landmark.MuscleGroup.Name,
                landmark.Mev,
                landmark.Mrv
            })
            .ToListAsync(cancellationToken);

        return landmarks
            .Select(landmark =>
            {
                var sets = volumeByMuscleGroupId.GetValueOrDefault(landmark.MuscleGroupId);

                return new WeeklyVolumeDto
                {
                    Muscle = landmark.Muscle,
                    Sets = sets,
                    Mev = landmark.Mev,
                    Mrv = landmark.Mrv,
                    Status = GetStatus(sets, landmark.Mev, landmark.Mrv)
                };
            })
            .ToList();
    }

    private static string GetStatus(decimal sets, int mev, int mrv)
    {
        if (sets < mev)
        {
            return "below";
        }

        return sets > mrv ? "above" : "optimal";
    }
}
