using StrengthPlanner.Application.DTOs.Analytics;

namespace StrengthPlanner.Application.Interfaces;

public interface IVolumeService
{
    Task<IReadOnlyList<WeeklyVolumeDto>> GetWeeklyVolumeAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        CancellationToken cancellationToken = default);
}
