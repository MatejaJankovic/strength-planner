using StrengthPlanner.Application.DTOs.Sessions;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>Every finished and skipped workout of the user, across all plans and blocks.</summary>
public interface ITrainingHistoryService
{
    Task<TrainingHistoryDto> GetAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
