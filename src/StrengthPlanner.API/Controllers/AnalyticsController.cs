using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Analytics;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics")]
public class AnalyticsController : AuthorizedControllerBase
{
    private readonly IVolumeService _volumeService;
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IVolumeService volumeService, IAnalyticsService analyticsService)
    {
        _volumeService = volumeService;
        _analyticsService = analyticsService;
    }

    /// <summary>Vraća nedeljni volumen po mišićnoj grupi.</summary>
    [HttpGet("volume")]
    [ProducesResponseType(typeof(IReadOnlyList<WeeklyVolumeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeeklyVolume(
        [FromQuery] Guid mesocycleId,
        [FromQuery(Name = "week")] int weekNumber,
        CancellationToken cancellationToken)
    {
        var volume = await _volumeService.GetWeeklyVolumeAsync(GetUserId(), mesocycleId, weekNumber, cancellationToken);
        return Ok(volume);
    }

    /// <summary>Vraća e1RM trend za vežbu.</summary>
    [HttpGet("e1rm/{exerciseId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<E1RmTrendPointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetE1rmTrend(Guid exerciseId, CancellationToken cancellationToken)
    {
        var trend = await _analyticsService.GetE1rmTrendAsync(GetUserId(), exerciseId, cancellationToken);
        return Ok(trend);
    }

    /// <summary>Vraća personal records po vežbi.</summary>
    [HttpGet("prs")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonalRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonalRecords(CancellationToken cancellationToken)
    {
        var records = await _analyticsService.GetPersonalRecordsAsync(GetUserId(), cancellationToken);
        return Ok(records);
    }
}
