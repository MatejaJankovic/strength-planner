using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Security;

/// <summary>
/// Čita identitet korisnika iz <c>sub</c> claim-a tokena koji je već prošao validaciju
/// potpisa, isteka i security stamp-a. Isti izvor koji koristi
/// <see cref="Controllers.AuthorizedControllerBase.GetUserId"/>, samo dostupan i sloju
/// podataka.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            // Neautentifikovan zahtev ne sme da dobije identitet iz claim-ova koje je sam
            // poslao: bez ove provere bi neispravan token čiji su claim-ovi ipak
            // parsirani mogao da otvori tuđe redove.
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }
}
