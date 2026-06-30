using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/mesocycles")]
public class MesocyclesController : AuthorizedControllerBase
{
    private readonly IMesocycleGenerator _mesocycleGenerator;
    private readonly IMesocycleService _mesocycleService;

    public MesocyclesController(IMesocycleGenerator mesocycleGenerator, IMesocycleService mesocycleService)
    {
        _mesocycleGenerator = mesocycleGenerator;
        _mesocycleService = mesocycleService;
    }

    /// <summary>Generiše novi četvoronedeljni mezociklus.</summary>
    /// <remarks>Primer body-ja: { "templateKey": "full-body", "goal": "Hypertrophy", "name": "Base Hypertrophy", "startDate": "2026-07-06" }</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(MesocycleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate(
        GenerateMesocycleRequest request,
        CancellationToken cancellationToken)
    {
        var mesocycle = await _mesocycleGenerator.GenerateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = mesocycle.Id }, mesocycle);
    }

    /// <summary>Vraća listu mezociklusa ulogovanog korisnika.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MesocycleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var mesocycles = await _mesocycleService.GetAllAsync(GetUserId(), cancellationToken);
        return Ok(mesocycles);
    }

    /// <summary>Vraća aktivni mezociklus sa punom strukturom.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(MesocycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var mesocycle = await _mesocycleService.GetActiveAsync(GetUserId(), cancellationToken);
        return Ok(mesocycle);
    }

    /// <summary>Vraća pun mezociklus: weeks -> sessions -> plans -> logs.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MesocycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var mesocycle = await _mesocycleService.GetByIdAsync(GetUserId(), id, cancellationToken);
        return Ok(mesocycle);
    }

    /// <summary>Briše mezociklus ulogovanog korisnika.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mesocycleService.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
