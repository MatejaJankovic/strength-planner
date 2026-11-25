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
}
