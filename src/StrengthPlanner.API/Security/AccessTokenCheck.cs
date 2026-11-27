using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.API.Security;

/// <summary>
/// What a correctly signed, unexpired access token still has to pass.
///
/// A JWT by itself is stateless: it would stay valid until it expires, after a sign-out and after a
/// password change alike. So two things are read on every request, at the price of two queries: the
/// Identity security stamp, which changes with the password, and the sign-in the token names, which a
/// sign-out deletes.
/// </summary>
public sealed class AccessTokenCheck
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILoginSessionService _sessions;

    public AccessTokenCheck(UserManager<ApplicationUser> users, ILoginSessionService sessions)
    {
        _users = users;
        _sessions = sessions;
    }

    /// <summary>Why the token must be refused, or null when it may be accepted.</summary>
    public async Task<string?> RefusalAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var stampInToken = principal?.FindFirst(JwtTokenService.SecurityStampClaim)?.Value;
        var subject = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var session = principal?.FindFirst(JwtTokenService.SessionIdClaim)?.Value;

        if (string.IsNullOrEmpty(stampInToken)
            || !Guid.TryParse(subject, out var userId)
            || !Guid.TryParse(session, out var sessionId))
        {
            return "Token ne nosi podatke potrebne za proveru.";
        }

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || !string.Equals(user.SecurityStamp, stampInToken, StringComparison.Ordinal))
        {
            return "Token više ne važi.";
        }

        return await _sessions.IsActiveAsync(sessionId, userId, cancellationToken)
            ? null
            : "Prijava je završena.";
    }
}
