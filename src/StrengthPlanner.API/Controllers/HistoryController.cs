using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/history")]
public class HistoryController : AuthorizedControllerBase
{
    private readonly ITrainingHistoryService _history;

    public HistoryController(ITrainingHistoryService history)
    {
        _history = history;
    }

    /// <summary>Finished and skipped workouts across all plans and blocks, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(TrainingHistoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await _history.GetAsync(GetUserId(), page, pageSize, cancellationToken);
        return Ok(history);
    }
}
