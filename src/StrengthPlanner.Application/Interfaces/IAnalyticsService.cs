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

    /// <summary>
    /// Tonnage and stimulative sets by calendar week across all plans and blocks.
    /// <paramref name="weeks"/> is how many weeks back from the current one (at least 1); null is all
    /// history. The weeks before the first set in the range are not part of the result.
    /// </summary>
    Task<TrendDto> GetTrendAsync(
        Guid userId,
        int? weeks,
        CancellationToken cancellationToken = default);
}
