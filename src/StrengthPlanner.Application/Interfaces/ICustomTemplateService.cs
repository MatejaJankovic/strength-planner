using StrengthPlanner.Application.DTOs.Templates;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>Lični šabloni treninga: pravljenje, izmena i brisanje.</summary>
public interface ICustomTemplateService
{
    Task<IReadOnlyList<CustomTemplateDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CustomTemplateDto> GetByIdAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task<CustomTemplateDto> CreateAsync(
        Guid userId,
        SaveCustomTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomTemplateDto> UpdateAsync(
        Guid userId,
        Guid templateId,
        SaveCustomTemplateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Briše šablon. Odbija se ako ga neki blok dugoročnog plana još čeka: taj blok se
    /// generiše tek kada mu dođe red, pa bi bez šablona ostao neupotrebljiv.
    /// </summary>
    Task DeleteAsync(Guid userId, Guid templateId, CancellationToken cancellationToken = default);
}
