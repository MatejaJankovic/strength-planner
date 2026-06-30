using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class SetLogsController : AuthorizedControllerBase
{
    private readonly ISetLogService _setLogService;

    public SetLogsController(ISetLogService setLogService)
    {
        _setLogService = setLogService;
    }

    /// <summary>Loguje jednu radnu seriju za plan vežbe.</summary>
    /// <remarks>Primer body-ja: { "weightKg": 80, "reps": 8, "rir": 2 }</remarks>
    [HttpPost("exercise-plans/{planId:guid}/sets")]
    [ProducesResponseType(typeof(SetLogDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSet(
        Guid planId,
        AddSetLogRequest request,
        CancellationToken cancellationToken)
    {
        var setLog = await _setLogService.AddSetAsync(GetUserId(), planId, request, cancellationToken);
        return Created($"/api/sets/{setLog.Id}", setLog);
    }

    /// <summary>Menja logovanu radnu seriju.</summary>
    [HttpPut("sets/{id:guid}")]
    [ProducesResponseType(typeof(SetLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSet(
        Guid id,
        UpdateSetLogRequest request,
        CancellationToken cancellationToken)
    {
        var setLog = await _setLogService.UpdateSetAsync(GetUserId(), id, request, cancellationToken);
        return Ok(setLog);
    }

    /// <summary>Briše logovanu radnu seriju.</summary>
    [HttpDelete("sets/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSet(Guid id, CancellationToken cancellationToken)
    {
        await _setLogService.DeleteSetAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
