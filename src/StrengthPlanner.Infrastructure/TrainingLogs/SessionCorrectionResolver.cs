using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Reads from the database what <see cref="SessionCorrection"/> needs to decide whether a
/// finished workout can still be corrected. The rule itself is in the domain; this is only the
/// two questions it has to be asked about the neighbours of the workout.
/// </summary>
public static class SessionCorrectionResolver
{
    /// <summary>
    /// Null when the workout does not exist or is not the user's.
    /// </summary>
    public static async Task<CorrectionWindow?> ResolveAsync(
        AppDbContext db,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await db.WorkoutSessions
            .AsNoTracking()
            .Where(candidate => candidate.Id == sessionId
                                && candidate.TrainingWeek.Mesocycle.UserId == userId)
            .Select(candidate => new
            {
                candidate.Status,
                Recorded = candidate.CompletedAt != null,
                candidate.DayLabel,
                candidate.TrainingWeek.WeekNumber,
                candidate.TrainingWeek.MesocycleId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.Status != SessionStatus.Completed)
        {
            return SessionCorrection.Evaluate(session.Status, session.Recorded, NextWorkoutState.None, false);
        }

        // Ista oznaka dana u najbližoj kasnijoj nedelji: tu odlazi cilj ovog treninga.
        var nextStatus = await db.WorkoutSessions
            .AsNoTracking()
            .Where(candidate => candidate.TrainingWeek.MesocycleId == session.MesocycleId
                                && candidate.TrainingWeek.Mesocycle.UserId == userId
                                && candidate.DayLabel == session.DayLabel
                                && candidate.TrainingWeek.WeekNumber > session.WeekNumber)
            .OrderBy(candidate => candidate.TrainingWeek.WeekNumber)
            .Select(candidate => (SessionStatus?)candidate.Status)
            .FirstOrDefaultAsync(cancellationToken);

        var next = SessionCorrection.NextWorkoutOf(nextStatus);

        // Blok sledi iz procena samo kad nema kasnije nedelje; tek tada je važno da li je
        // već generisan.
        var nextBlockOpened = next == NextWorkoutState.None
                              && await NextBlockOpenedAsync(db, userId, session.MesocycleId, cancellationToken);

        return SessionCorrection.Evaluate(session.Status, session.Recorded, next, nextBlockOpened);
    }

    private static async Task<bool> NextBlockOpenedAsync(
        AppDbContext db,
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        var block = await db.MacrocycleBlocks
            .AsNoTracking()
            .Where(candidate => candidate.MesocycleId == mesocycleId
                                && candidate.Macrocycle.UserId == userId)
            .Select(candidate => new { candidate.MacrocycleId, candidate.Order })
            .FirstOrDefaultAsync(cancellationToken);

        if (block is null)
        {
            return false;
        }

        return await db.MacrocycleBlocks
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.MacrocycleId == block.MacrocycleId
                             && candidate.Macrocycle.UserId == userId
                             && candidate.Order > block.Order
                             && candidate.MesocycleId != null,
                cancellationToken);
    }
}
