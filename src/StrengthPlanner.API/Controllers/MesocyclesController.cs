using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

/// <summary>
/// Čitanje mezociklusa. Pravljenja i brisanja ovde nema: mezociklus je blok dugoročnog
/// plana i nastaje sa njim, pa se i pravi i briše preko <c>api/macrocycles</c>.
///
/// Ranije je ovde stajao <c>POST</c> koji je pravio plan sa jednim blokom i vraćao njegov
/// mezociklus. Taj put je bio samo drugo lice istog posla - i baza to nikada nije videla
/// drugačije - ali je u aplikaciji davao drugi ekran za istu stvar, sa kog se zatečeni plan
/// gasio bez upozorenja.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mesocycles")]
public class MesocyclesController : AuthorizedControllerBase
{
    private readonly IMesocycleService _mesocycleService;

    public MesocyclesController(IMesocycleService mesocycleService)
    {
        _mesocycleService = mesocycleService;
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
}
