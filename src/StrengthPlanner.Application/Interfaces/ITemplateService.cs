using StrengthPlanner.Application.DTOs.Templates;

namespace StrengthPlanner.Application.Interfaces;

public interface ITemplateService
{
    /// <summary>
    /// Ugrađeni šabloni, prilagođeni korisnikovom profilu: dani su skraćeni na njegov
    /// nivo iskustva, a šablon koji odgovara broju njegovih trenažnih dana je označen.
    /// </summary>
    Task<IReadOnlyList<WorkoutTemplateDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
