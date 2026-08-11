using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Analytics;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

public class VolumeService : IVolumeService
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _landmarks;

    public VolumeService(AppDbContext db, VolumeLandmarkService landmarks)
    {
        _db = db;
        _landmarks = landmarks;
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

        var muscleNames = await _db.MuscleGroups
            .AsNoTracking()
            .ToDictionaryAsync(group => group.Id, group => group.Name, cancellationToken);
        var effective = await _landmarks.GetEffectiveAsync(userId, cancellationToken);

        return effective
            .Select(entry =>
            {
                var sets = volumeByMuscleGroupId.GetValueOrDefault(entry.Key);
                var landmark = entry.Value;

                return new WeeklyVolumeDto
                {
                    Muscle = muscleNames.GetValueOrDefault(entry.Key, string.Empty),
                    Sets = sets,
                    Mev = landmark.Mev,
                    Mrv = landmark.Mrv,
                    DefaultMev = landmark.SeedMev,
                    DefaultMrv = landmark.SeedMrv,
                    IsPersonal = landmark.IsPersonal,
                    Status = GetStatus(sets, landmark.Mev, landmark.Mrv)
                };
            })
            .OrderBy(dto => dto.Muscle)
            .ToList();
    }

    public async Task ResetLandmarksAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _landmarks.ResetAsync(userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
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
