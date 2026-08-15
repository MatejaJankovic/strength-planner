using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Templates;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

/// <summary>
/// Lični šabloni treninga. Svaki poziv radi nad korisnikom iz tokena; identifikator iz
/// zahteva se koristi samo uz njega, nikada sam.
/// </summary>
[ApiController]
[Authorize]
[Route("api/templates/custom")]
public class CustomTemplatesController : AuthorizedControllerBase
{
    private readonly ICustomTemplateService _customTemplateService;

    public CustomTemplatesController(ICustomTemplateService customTemplateService)
    {
        _customTemplateService = customTemplateService;
    }

    /// <summary>Svi lični šabloni korisnika, sa danima i vežbama.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _customTemplateService.GetAllAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _customTemplateService.GetByIdAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] SaveCustomTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var template = await _customTemplateService.CreateAsync(GetUserId(), request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] SaveCustomTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _customTemplateService.UpdateAsync(GetUserId(), id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _customTemplateService.DeleteAsync(GetUserId(), id, cancellationToken);

        return NoContent();
    }
}
