using StrengthPlanner.Application.DTOs.Analytics;

namespace StrengthPlanner.Application.Interfaces;

public interface IVolumeService
{
    Task<IReadOnlyList<WeeklyVolumeDto>> GetWeeklyVolumeAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vraća naučene MEV/MRV granice na populacione seed vrednosti.
    /// </summary>
    Task ResetLandmarksAsync(Guid userId, CancellationToken cancellationToken = default);
}
