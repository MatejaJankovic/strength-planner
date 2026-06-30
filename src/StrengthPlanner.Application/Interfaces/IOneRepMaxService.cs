using StrengthPlanner.Application.DTOs.OneRepMax;

namespace StrengthPlanner.Application.Interfaces;

public interface IOneRepMaxService
{
    Task<OneRepMaxDto> AddManualAsync(
        Guid userId,
        CreateOneRepMaxRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OneRepMaxDto>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OneRepMaxDto>> GetHistoryAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);
}
