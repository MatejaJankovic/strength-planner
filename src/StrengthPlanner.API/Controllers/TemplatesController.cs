using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.Templates;

namespace StrengthPlanner.API.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplatesController : AuthorizedControllerBase
{
    /// <summary>Vraća ugrađene trening šablone.</summary>
    [Authorize]
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(WorkoutTemplateCatalog.GetAll());
    }
}
