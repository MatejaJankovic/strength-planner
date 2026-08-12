using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplatesController : AuthorizedControllerBase
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    /// <summary>
    /// Vraća ugrađene trening šablone, skraćene na nivo iskustva iz profila, sa oznakom
    /// onog koji odgovara broju trenažnih dana.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _templateService.GetForUserAsync(GetUserId(), cancellationToken));
    }
}
