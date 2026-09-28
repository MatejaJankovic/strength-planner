using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.DTOs.Mesocycles;

namespace StrengthPlanner.Application.Interfaces;

public interface ISessionService
{
    Task<WorkoutSessionDto> GetByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<WorkoutSessionDto> StartAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CompleteSessionResultDto> CompleteAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a planned session as one that will not happen, so the week can close and the
    /// plan can move on. Reversible through <see cref="UnskipAsync"/>.
    /// </summary>
    Task<WorkoutSessionDto> SkipAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Puts a skipped session back on the plan.</summary>
    Task<WorkoutSessionDto> UnskipAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
