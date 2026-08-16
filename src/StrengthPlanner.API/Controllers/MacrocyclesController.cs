using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/macrocycles")]
public class MacrocyclesController : AuthorizedControllerBase
{
    private readonly IMacrocycleService _macrocycleService;

    public MacrocyclesController(IMacrocycleService macrocycleService)
    {
        _macrocycleService = macrocycleService;
    }

    /// <summary>Pravi dugoročan plan i generiše njegov prvi blok.</summary>
    /// <remarks>
    /// Primer body-ja:
    /// { "name": "Zima 2026", "startDate": "2026-09-01",
    ///   "blocks": [ { "goal": "Hypertrophy", "templateKey": "upper-lower" },
    ///               { "goal": "Strength", "templateKey": "upper-lower" } ] }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(MacrocycleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateMacrocycleRequest request,
        CancellationToken cancellationToken)
    {
        var macrocycle = await _macrocycleService.CreateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = macrocycle.Id }, macrocycle);
    }

    /// <summary>Predlog blokova sa smenjujućim ciljevima, za čarobnjak.</summary>
    [HttpGet("suggested-blocks")]
    [ProducesResponseType(typeof(IReadOnlyList<CreateMacrocycleBlockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSuggestedBlocks(
        CancellationToken cancellationToken,
        [FromQuery] int blockCount = 2,
        [FromQuery] Goal firstGoal = Goal.Hypertrophy,
        [FromQuery] string templateKey = "upper-lower")
    {
        return Ok(await _macrocycleService.SuggestBlocksAsync(
            GetUserId(),
            blockCount,
            firstGoal,
            templateKey,
            cancellationToken));
    }

    /// <summary>Vraća aktivan dugoročan plan sa svim blokovima.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(MacrocycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var macrocycle = await _macrocycleService.GetActiveAsync(GetUserId(), cancellationToken);
        return Ok(macrocycle);
    }

    /// <summary>Vraća plan po identifikatoru.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MacrocycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var macrocycle = await _macrocycleService.GetByIdAsync(GetUserId(), id, cancellationToken);
        return Ok(macrocycle);
    }

    /// <summary>
    /// Briše plan sa svim njegovim mezociklusima i odrađenim serijama u njima.
    /// Otkako se trening pravi samo kroz plan, ovo je jedini put do uklanjanja.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _macrocycleService.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
