using StrengthPlanner.Application.DTOs.SetLogs;

namespace StrengthPlanner.Application.Interfaces;

public interface ISetLogService
{
    Task<SetLogDto> AddSetAsync(
        Guid userId,
        Guid exercisePlanId,
        AddSetLogRequest request,
        CancellationToken cancellationToken = default);

    Task<SetLogDto> UpdateSetAsync(
        Guid userId,
        Guid setLogId,
        UpdateSetLogRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteSetAsync(
        Guid userId,
        Guid setLogId,
        CancellationToken cancellationToken = default);
}
