using StrengthPlanner.Application.DTOs.Sessions;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Saves the correction of a finished workout: works out again what finishing it produced.
/// </summary>
public interface ISessionRevisionService
{
    Task<SessionRevisionResultDto> ReviseAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves every correction of the block that was left unsaved. Called before anything that
    /// would use the sets up (starting, finishing or skipping a workout, which can also close
    /// the block), so a correction the lifter made and walked away from is never lost.
    /// </summary>
    Task ApplyPendingAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default);
}
