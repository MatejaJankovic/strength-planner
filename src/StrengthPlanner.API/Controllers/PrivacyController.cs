using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StrengthPlanner.API.Security;
using StrengthPlanner.Application.DTOs.Privacy;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.API.Controllers;

/// <summary>
/// Podaci koje politika privatnosti čita sa servera. Anonimno je: politiku mora da može da pročita i
/// neko ko tek odlučuje da li da napravi nalog.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/privacy")]
public class PrivacyController : AuthorizedControllerBase
{
    private readonly PrivacySettings _settings;

    public PrivacyController(IOptions<PrivacySettings> settings)
    {
        _settings = settings.Value;
    }

    [HttpGet]
    public ActionResult<PrivacyNoticeDto> Get()
    {
        return new PrivacyNoticeDto
        {
            Version = PrivacyPolicy.Version,
            ControllerName = Configured(_settings.ControllerName),
            ContactEmail = Configured(_settings.ContactEmail)
        };
    }

    private static string? Configured(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
