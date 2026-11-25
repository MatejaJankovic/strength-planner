using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.DTOs.Notes;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/sessions")]
public class SessionsController : AuthorizedControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ISessionRevisionService _revisions;
    private readonly IExerciseSubstitutionService _substitutions;
    private readonly IWorkoutNoteService _notes;
    private readonly IBlockEditService _blockEdits;

    public SessionsController(
        ISessionService sessionService,
        ISessionRevisionService revisions,
        IExerciseSubstitutionService substitutions,
        IWorkoutNoteService notes,
        IBlockEditService blockEdits)
    {
        _sessionService = sessionService;
        _revisions = revisions;
        _substitutions = substitutions;
        _notes = notes;
        _blockEdits = blockEdits;
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
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompleteSessionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sessionService.CompleteAsync(GetUserId(), id, request?.PerceivedFatigue, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Saves the correction of a finished workout: works out the estimate, the next target and
    /// the week's results again from the corrected sets.
    /// </summary>
    [HttpPost("{id:guid}/revise")]
    [ProducesResponseType(typeof(SessionRevisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revise(Guid id, CancellationToken cancellationToken)
    {
        var result = await _revisions.ReviseAsync(GetUserId(), id, cancellationToken);
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

    /// <summary>Vežbe koje smeju da se dodaju u dan ovog treninga, za ostatak bloka.</summary>
    [HttpGet("{id:guid}/addable-exercises")]
    [ProducesResponseType(typeof(AddableExercisesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Addable(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _blockEdits.GetAddableAsync(GetUserId(), id, cancellationToken));
    }

    /// <summary>
    /// Dodaje vežbu u ovaj i u svaki trening istog dana koji još čeka, do kraja bloka, i vraća
    /// trening u koji je dodata.
    /// </summary>
    [HttpPost("{id:guid}/exercises")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddExercise(
        Guid id,
        [FromBody] AddExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessionId = await _blockEdits.AddExerciseAsync(userId, id, request.ExerciseId, cancellationToken);
        return Ok(await _sessionService.GetByIdAsync(userId, sessionId, cancellationToken));
    }

    /// <summary>
    /// Uklanja vežbu iz ovog i iz svakog treninga istog dana koji još čeka, do kraja bloka, i
    /// vraća trening iz koga je uklonjena.
    /// </summary>
    [HttpDelete("plans/{planId:guid}")]
    [ProducesResponseType(typeof(WorkoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveExercise(Guid planId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessionId = await _blockEdits.RemoveExerciseAsync(userId, planId, cancellationToken);
        return Ok(await _sessionService.GetByIdAsync(userId, sessionId, cancellationToken));
    }

    /// <summary>
    /// Postavlja ili briše belešku uz trening. Dozvoljeno u svakom statusu, i na završenom i na
    /// preskočenom: beleška nije podatak o treningu, pa ništa ne preračunava i ne otvara ispravku.
    /// </summary>
    [HttpPut("{id:guid}/note")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNote(
        Guid id,
        SetNoteRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _notes.SetSessionNoteAsync(GetUserId(), id, request.Note, cancellationToken));
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
