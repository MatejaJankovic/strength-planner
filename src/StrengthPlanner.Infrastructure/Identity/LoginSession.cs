namespace StrengthPlanner.Infrastructure.Identity;

/// <summary>
/// One signed-in device. Lives next to <see cref="ApplicationUser"/> and not in the domain: no
/// training rule knows about it.
///
/// It carries no ownership query filter, like the Identity tables. It is read while the request has no
/// identity yet, so a filter on the current user would hide the row from the code that establishes
/// who the user is. It is only reached through <c>LoginSessionService</c>.
/// </summary>
public class LoginSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hash of the refresh token the client is known to hold.</summary>
    public string HeldTokenHash { get; set; } = string.Empty;

    /// <summary>Hash of the token issued after it, until the client shows that it received it.</summary>
    public string? IssuedTokenHash { get; set; }

    /// <summary>
    /// The account's security stamp at sign-in. Identity changes the stamp with the password, which
    /// ends the session at its next refresh whatever changed the password.
    /// </summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
