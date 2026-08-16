using StrengthPlanner.Application.DTOs.Mesocycles;

namespace StrengthPlanner.Application.Interfaces;

public interface IMesocycleService
{
    Task<IReadOnlyList<MesocycleSummaryDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MesocycleDto> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MesocycleDto> GetByIdAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default);
}
