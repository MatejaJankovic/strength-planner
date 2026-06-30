using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.OneRepMax;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/onerepmax")]
public class OneRepMaxController : AuthorizedControllerBase
{
    private readonly IOneRepMaxService _oneRepMaxService;

    public OneRepMaxController(IOneRepMaxService oneRepMaxService)
    {
        _oneRepMaxService = oneRepMaxService;
    }

    /// <summary>Upisuje ručni 1RM za vežbu.</summary>
    /// <remarks>Primer body-ja: { "exerciseId": "00000000-0000-0000-0000-000000000000", "valueKg": 120 }</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(OneRepMaxDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateManual(
        CreateOneRepMaxRequest request,
        CancellationToken cancellationToken)
    {
        var record = await _oneRepMaxService.AddManualAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetHistory), new { exerciseId = record.ExerciseId }, record);
    }

    /// <summary>Vraća najnoviji 1RM po vežbi za ulogovanog korisnika.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OneRepMaxDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var records = await _oneRepMaxService.GetCurrentAsync(GetUserId(), cancellationToken);
        return Ok(records);
    }

    /// <summary>Vraća istoriju 1RM zapisa za jednu vežbu.</summary>
    [HttpGet("{exerciseId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<OneRepMaxDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(Guid exerciseId, CancellationToken cancellationToken)
    {
        var records = await _oneRepMaxService.GetHistoryAsync(GetUserId(), exerciseId, cancellationToken);
        return Ok(records);
    }
}
