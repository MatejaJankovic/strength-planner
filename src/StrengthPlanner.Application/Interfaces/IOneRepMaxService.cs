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

    /// <summary>
    /// Estimates a one-rep max from a test set the lifter reports, and stores it as an
    /// estimate — because that is what it is. It competes with estimates the app derived
    /// from logged sets under the same rule, rather than overruling them the way a typed
    /// maximum does.
    /// </summary>
    Task<OneRepMaxDto> AddFromSetAsync(
        Guid userId,
        CreateOneRepMaxFromSetRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OneRepMaxDto>> GetHistoryAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one record the lifter owns. The screen shows the value the plan starts
    /// from, so this is how a number typed by mistake stops being that value.
    /// </summary>
    Task DeleteAsync(
        Guid userId,
        Guid recordId,
        CancellationToken cancellationToken = default);
}
