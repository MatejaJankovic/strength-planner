using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Authentication;

/// <summary>Stores sign-ins and applies <see cref="RefreshTokenPolicy"/> to them.</summary>
public class LoginSessionService : ILoginSessionService
{
    /// <summary>
    /// Attempts to write a rotation before giving up. A write is lost only to another request for the
    /// same session that got in between, which a client that refreshes one at a time rarely causes.
    /// </summary>
    private const int RotationAttempts = 3;

    private readonly AppDbContext _db;
    private readonly JwtSettings _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<LoginSessionService> _logger;

    public LoginSessionService(
        AppDbContext db,
        IOptions<JwtSettings> settings,
        TimeProvider clock,
        ILogger<LoginSessionService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<LoginSessionTicket> StartAsync(
        Guid userId,
        string securityStamp,
        CancellationToken cancellationToken = default)
    {
        var now = Now();

        // Istekle sesije niko više ne može da iskoristi; brišu se ovde, pri sledećoj prijavi
        // istog naloga, da tabela ne raste bez granice.
        await _db.LoginSessions
            .Where(session => session.UserId == userId && session.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var id = Guid.NewGuid();
        var (token, hash) = RefreshTokenPolicy.Issue(id);
        var session = new LoginSession
        {
            Id = id,
            UserId = userId,
            HeldTokenHash = hash,
            SecurityStamp = securityStamp,
            CreatedAtUtc = now,
            ExpiresAtUtc = ExpiryFrom(now)
        };

        _db.LoginSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoginSessionTicket(id, userId, token, session.ExpiresAtUtc);
    }

    public async Task<LoginSessionTicket?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!RefreshTokenPolicy.TryRead(refreshToken, out var sessionId, out var presentedHash))
        {
            return null;
        }

        for (var attempt = 0; attempt < RotationAttempts; attempt++)
        {
            var session = await _db.LoginSessions.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);
            if (session is null)
            {
                return null;
            }

            var now = Now();
            var stamp = await _db.Users.AsNoTracking()
                .Where(user => user.Id == session.UserId)
                .Select(user => user.SecurityStamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (session.ExpiresAtUtc <= now
                || !string.Equals(stamp, session.SecurityStamp, StringComparison.Ordinal))
            {
                await DeleteAsync(sessionId, cancellationToken);
                return null;
            }

            var use = RefreshTokenPolicy.Classify(presentedHash, session.HeldTokenHash, session.IssuedTokenHash);
            if (use == RefreshTokenUse.Reused)
            {
                _logger.LogWarning(
                    "A replaced refresh token was presented for session {SessionId} of user {UserId}; "
                    + "the session was ended.",
                    sessionId,
                    session.UserId);
                await DeleteAsync(sessionId, cancellationToken);
                return null;
            }

            // Token koji je upravo stigao je onaj za koji se zna da ga klijent ima.
            var held = presentedHash;
            var (token, issued) = RefreshTokenPolicy.Issue(sessionId);
            var expires = ExpiryFrom(now);
            var previousHeld = session.HeldTokenHash;
            var previousIssued = session.IssuedTokenHash;

            // Upis prolazi samo ako se red od čitanja nije promenio. Bez ovog uslova bi dva
            // istovremena zahteva upisala jedan preko drugog i klijent bi dobio token koji
            // sesija više ne prima.
            var written = await _db.LoginSessions
                .Where(candidate => candidate.Id == sessionId
                                    && candidate.HeldTokenHash == previousHeld
                                    && candidate.IssuedTokenHash == previousIssued)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(candidate => candidate.HeldTokenHash, held)
                        .SetProperty(candidate => candidate.IssuedTokenHash, issued)
                        .SetProperty(candidate => candidate.ExpiresAtUtc, expires),
                    cancellationToken);

            if (written == 1)
            {
                return new LoginSessionTicket(sessionId, session.UserId, token, expires);
            }
        }

        return null;
    }

    public async Task EndAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!RefreshTokenPolicy.TryRead(refreshToken, out var sessionId, out var presentedHash))
        {
            return;
        }

        var session = await _db.LoginSessions.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);

        // Bez tajne se ne odjavljuje: id sesije stoji i u pristupnom tokenu.
        if (session is null
            || RefreshTokenPolicy.Classify(presentedHash, session.HeldTokenHash, session.IssuedTokenHash)
                == RefreshTokenUse.Reused)
        {
            return;
        }

        await DeleteAsync(sessionId, cancellationToken);
    }

    public Task EndAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _db.LoginSessions
            .Where(session => session.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = Now();

        return _db.LoginSessions.AnyAsync(
            session => session.Id == sessionId && session.UserId == userId && session.ExpiresAtUtc > now,
            cancellationToken);
    }

    private Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _db.LoginSessions
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private DateTime Now()
    {
        return _clock.GetUtcNow().UtcDateTime;
    }

    private DateTime ExpiryFrom(DateTime now)
    {
        return now.AddDays(_settings.RefreshExpiryDays);
    }
}
