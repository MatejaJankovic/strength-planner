using StrengthPlanner.Application.DTOs.Analytics;

namespace StrengthPlanner.Application.Interfaces;

public interface IAnalyticsService
{
    Task<IReadOnlyList<E1RmTrendPointDto>> GetE1rmTrendAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalRecordDto>> GetPersonalRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Ukupna tonaža po nedeljama jednog mezociklusa (zbir težina x ponavljanja).</summary>
    Task<IReadOnlyList<WeeklyTonnageDto>> GetWeeklyTonnageAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken = default);
}
