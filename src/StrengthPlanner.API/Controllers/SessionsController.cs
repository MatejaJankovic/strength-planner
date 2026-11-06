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
    private readonly IExerciseSubstitutionService _substitutions;

    public SessionsController(ISessionService sessionService, IExerciseSubstitutionService substitutions)
    {
        _sessionService = sessionService;
        _substitutions = substitutions;
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

    /// <summary>Označava planiran trening kao preskočen, da nedelja može da se zatvori.</summary>
    [HttpPost("{id:guid}/skip")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Skip(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionService.SkipAsync(GetUserId(), id, cancellationToken);
        return Ok(session);
    }

    /// <summary>Vežbe koje mogu da zamene planiranu za ostatak bloka.</summary>
    [HttpGet("plans/{planId:guid}/alternatives")]
    [ProducesResponseType(typeof(IReadOnlyList<ExerciseAlternativeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Alternatives(Guid planId, CancellationToken cancellationToken)
    {
        return Ok(await _substitutions.GetAlternativesAsync(GetUserId(), planId, cancellationToken));
    }

    /// <summary>
    /// Menja vežbu u ovom i u svakom treningu istog dana koji još čeka, do kraja bloka, i vraća
    /// trening iz koga je zamena tražena.
    /// </summary>
    [HttpPost("plans/{planId:guid}/substitute")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Substitute(
        Guid planId,
        [FromBody] SubstituteExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessionId = await _substitutions.SubstituteAsync(userId, planId, request.ExerciseId, cancellationToken);
        return Ok(await _sessionService.GetByIdAsync(userId, sessionId, cancellationToken));
    }

    /// <summary>Vraća preskočen trening na plan.</summary>
    [HttpPost("{id:guid}/unskip")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unskip(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionService.UnskipAsync(GetUserId(), id, cancellationToken);
        return Ok(session);
    }
}
