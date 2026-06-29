using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StrengthPlanner.API.Controllers;

[Authorize]
public abstract class AuthorizedControllerBase : ControllerBase
{
    protected Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("JWT user id claim is missing or invalid.");
        }

        return userId;
    }
}
