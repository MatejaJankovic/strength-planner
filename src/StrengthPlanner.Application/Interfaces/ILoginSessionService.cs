namespace StrengthPlanner.Application.Interfaces;

/// <summary>A signed-in device: what the client gets to keep, and until when it works.</summary>
public sealed record LoginSessionTicket(Guid SessionId, Guid UserId, string RefreshToken, DateTime ExpiresAtUtc);

/// <summary>
/// Sign-ins that outlive the one-hour access token, one per device. See
/// <see cref="Security.RefreshTokenPolicy"/> for how their tokens rotate.
///
/// These methods are the one place that reads a user's rows before the request has an identity: a
/// refresh arrives with an expired access token or none. Everything is therefore found by the
/// session id and proven by the secret, never by anything the client says about who it is.
/// </summary>
public interface ILoginSessionService
{
    /// <summary>Opens a session for an account that has just proven its password.</summary>
    Task<LoginSessionTicket> StartAsync(
        Guid userId,
        string securityStamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for its successor and extends the session. Null when the token no
    /// longer signs anyone in: unknown, expired, replaced, or issued before a password change.
    /// </summary>
    Task<LoginSessionTicket?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Signs out the device that holds this token. A token that matches nothing is ignored.</summary>
    Task EndAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Signs out every device of the account.</summary>
    Task EndAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the session an access token names is still open and belongs to that account. Checked on
    /// every request, so that signing out takes effect at once and not when the token expires.
    /// </summary>
    Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}
