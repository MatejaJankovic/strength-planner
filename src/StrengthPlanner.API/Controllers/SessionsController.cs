using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/sessions")]
public class SessionsController : AuthorizedControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>Vraća detalj sesije sa planovima i logovima.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetByIdAsync(GetUserId(), id, cancellationToken);
        return Ok(session);
    }

    /// <summary>Postavlja sesiju u InProgress.</summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionService.StartAsync(GetUserId(), id, cancellationToken);
        return Ok(session);
    }

    /// <summary>Završava sesiju i pokreće e1RM/progression engine.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(CompleteSessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sessionService.CompleteAsync(GetUserId(), id, cancellationToken);
        return Ok(result);
    }
}
