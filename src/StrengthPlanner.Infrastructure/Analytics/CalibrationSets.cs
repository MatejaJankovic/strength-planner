using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Which logged sets of a block are calibration sets (<see cref="CalibrationSet"/>).
///
/// Three readers judge a week from its sets: the volume limits, and the fatigue score with its
/// strength term. A prescribed failure is evidence of none of what they ask: it is not
/// fatigue, and its reading is not comparable with the reserve-judged sets of the week before
/// (by the same rep of RIR error the calibration exists to remove, it would read about 2%
/// stronger). So every reader leaves the same sets out, and that answer lives here, once.
/// </summary>
public static class CalibrationSets
{
    /// <summary>Ids of the calibration sets logged in a block; empty when it has none.</summary>
    public static async Task<HashSet<Guid>> IdsAsync(
        AppDbContext db,
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var level = await db.Mesocycles
            .AsNoTracking()
            .Where(mesocycle => mesocycle.Id == mesocycleId && mesocycle.UserId == userId)
            .Select(mesocycle => (ExperienceLevel?)mesocycle.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken);

        if (level is null or ExperienceLevel.Beginner)
        {
            return [];
        }

        var weeks = await db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId && week.Mesocycle.UserId == userId)
            .Select(week => new { week.WeekNumber, week.IsDeload })
            .ToListAsync(cancellationToken);
        var lastTrainingWeek = weeks
            .Where(week => CalibrationSet.IsLastTrainingWeek(
                week.WeekNumber,
                week.IsDeload,
                weeks.Select(other => (other.WeekNumber, other.IsDeload))))
            .Select(week => (int?)week.WeekNumber)
            .FirstOrDefault();

        if (lastTrainingWeek is null)
        {
            return [];
        }

        var candidates = await db.SetLogs
            .AsNoTracking()
            .Where(set => set.SetNumber == CalibrationSet.SetNumber
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == lastTrainingWeek
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new
            {
                set.Id,
                set.ExercisePlan.Order,
                set.ExercisePlan.Exercise.Type,
                set.ExercisePlan.Exercise.SuitsLowReps,
                set.ExercisePlan.RepRangeMax,
                set.ExercisePlan.TargetRir
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(set => CalibrationSet.AppliesTo(
                level.Value,
                isLastTrainingWeek: true,
                set.Order,
                set.Type,
                set.SuitsLowReps,
                set.RepRangeMax,
                set.TargetRir))
            .Select(set => set.Id)
            .ToHashSet();
    }
}
