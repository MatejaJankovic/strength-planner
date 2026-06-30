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
}
