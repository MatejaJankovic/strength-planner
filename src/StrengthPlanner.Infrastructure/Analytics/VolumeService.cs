using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Analytics;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
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

        // Sabiranje se radi u memoriji jer stimulativni udeo živi kao domensko pravilo,
        // a ne kao izraz koji EF ume da prevede u SQL — bolje jedan izvor istine nego
        // isto pravilo napisano drugi put kao CASE.
        var contributions = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == weekNumber
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .SelectMany(set => set.ExercisePlan.Exercise.Muscles
                .Select(muscle => new
                {
                    muscle.MuscleGroupId,
                    muscle.Contribution,
                    set.Rir,
                    set.IsFailure
                }))
            .ToListAsync(cancellationToken);

        var volumeByMuscleGroupId = contributions
            .GroupBy(item => item.MuscleGroupId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.Contribution * StimulativeVolume.CreditFor(item.Rir, item.IsFailure)));

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
                    Mav = landmark.Mav,
                    Mrv = landmark.Mrv,
                    DefaultMev = landmark.SeedMev,
                    DefaultMav = landmark.SeedMav,
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
