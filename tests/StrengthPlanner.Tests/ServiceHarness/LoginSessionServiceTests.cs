using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Signing in, staying signed in and signing out, against a real context. The rules about which
/// token is accepted are in <c>RefreshTokenPolicyTests</c>; here it matters what is stored.
/// </summary>
public class LoginSessionServiceTests
{
    private const string Stamp = "stamp-at-sign-in";

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static SqliteDatabase NewDatabase()
    {
        var database = new SqliteDatabase();
        SetStamp(database, database.UserId, Stamp);
        return database;
    }

    private static void SetStamp(SqliteDatabase database, Guid userId, string stamp)
    {
        using var db = database.NewContext();
        db.Users.Single(user => user.Id == userId).SecurityStamp = stamp;
        db.SaveChanges();
    }

    private static LoginSessionService Service(AppDbContext db, TimeProvider clock)
    {
        return new LoginSessionService(
            db,
            Options.Create(new JwtSettings()),
            clock,
            NullLogger<LoginSessionService>.Instance);
    }

    private static async Task<LoginSessionTicket> SignIn(SqliteDatabase database, TimeProvider clock)
    {
        await using var db = database.NewContext();
        return await Service(db, clock).StartAsync(database.UserId, Stamp);
    }

    private static async Task<LoginSessionTicket?> Refresh(
        SqliteDatabase database, TimeProvider clock, string token)
    {
        await using var db = database.NewContext();
        return await Service(db, clock).RefreshAsync(token);
    }

    private static async Task<bool> IsActive(
        SqliteDatabase database, TimeProvider clock, Guid sessionId, Guid userId)
    {
        await using var db = database.NewContext();
        return await Service(db, clock).IsActiveAsync(sessionId, userId);
    }

    [Fact]
    public async Task ARefresh_HandsOutANewTokenForTheSameSession()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var first = await SignIn(database, clock);

        var second = await Refresh(database, clock, first.RefreshToken);
        var third = await Refresh(database, clock, second!.RefreshToken);

        Assert.NotNull(third);
        Assert.Equal(first.SessionId, third.SessionId);
        Assert.Equal(database.UserId, third.UserId);
        Assert.Equal(3, new[] { first.RefreshToken, second.RefreshToken, third.RefreshToken }.Distinct().Count());
        await using var db = database.NewContext();
        var stored = Assert.Single(db.LoginSessions);
        Assert.DoesNotContain(stored.HeldTokenHash, third.RefreshToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A phone in a gym loses answers; the token it still holds has to keep working.</summary>
    [Fact]
    public async Task ATokenWhoseAnswerWasLost_StillWorks_HoweverOften()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var first = await SignIn(database, clock);

        Assert.NotNull(await Refresh(database, clock, first.RefreshToken));
        clock.Now = clock.Now.AddDays(3);
        Assert.NotNull(await Refresh(database, clock, first.RefreshToken));
        var received = await Refresh(database, clock, first.RefreshToken);

        Assert.NotNull(await Refresh(database, clock, received!.RefreshToken));
    }

    [Fact]
    public async Task AReplacedToken_EndsTheSession()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var first = await SignIn(database, clock);
        var second = await Refresh(database, clock, first.RefreshToken);
        var third = await Refresh(database, clock, second!.RefreshToken);

        Assert.Null(await Refresh(database, clock, first.RefreshToken));
        Assert.Null(await Refresh(database, clock, third!.RefreshToken));
        Assert.False(await IsActive(database, clock, first.SessionId, database.UserId));
    }

    /// <summary>Someone copies the token and uses it before its owner does.</summary>
    [Fact]
    public async Task AStolenToken_UsedFirstByTheThief_EndsTheSessionWhenBothHaveUsedIt()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var owned = await SignIn(database, clock);

        var thiefs = await Refresh(database, clock, owned.RefreshToken);
        var owners = await Refresh(database, clock, owned.RefreshToken);
        Assert.NotNull(owners);

        Assert.Null(await Refresh(database, clock, thiefs!.RefreshToken));
        Assert.Null(await Refresh(database, clock, owners.RefreshToken));
    }

    [Fact]
    public async Task AStolenToken_ConfirmedByTheThief_IsCaughtWhenTheOwnerReturns()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var owned = await SignIn(database, clock);

        var thiefs = await Refresh(database, clock, owned.RefreshToken);
        var thiefsNext = await Refresh(database, clock, thiefs!.RefreshToken);

        Assert.Null(await Refresh(database, clock, owned.RefreshToken));
        Assert.Null(await Refresh(database, clock, thiefsNext!.RefreshToken));
    }

    [Fact]
    public async Task ASessionLeftAloneForThirtyDays_Expires()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);

        clock.Now = clock.Now.AddDays(30);

        Assert.False(await IsActive(database, clock, ticket.SessionId, database.UserId));
        Assert.Null(await Refresh(database, clock, ticket.RefreshToken));
        await using var db = database.NewContext();
        Assert.Empty(db.LoginSessions);
    }

    [Fact]
    public async Task EveryRefresh_StartsTheThirtyDaysAgain()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);

        for (var visit = 0; visit < 4; visit++)
        {
            clock.Now = clock.Now.AddDays(29);
            ticket = (await Refresh(database, clock, ticket.RefreshToken))!;
            Assert.NotNull(ticket);
            Assert.Equal(clock.Now.UtcDateTime.AddDays(30), ticket.ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task AChangedSecurityStamp_EndsTheSessionAtItsNextRefresh()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);

        SetStamp(database, database.UserId, "stamp-after-password-change");

        Assert.Null(await Refresh(database, clock, ticket.RefreshToken));
        await using var db = database.NewContext();
        Assert.Empty(db.LoginSessions);
    }

    [Fact]
    public async Task SigningOut_EndsOnlyThatDevice()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var phone = await SignIn(database, clock);
        var computer = await SignIn(database, clock);

        await using (var db = database.NewContext())
        {
            await Service(db, clock).EndAsync(phone.RefreshToken);
        }

        Assert.False(await IsActive(database, clock, phone.SessionId, database.UserId));
        Assert.Null(await Refresh(database, clock, phone.RefreshToken));
        Assert.True(await IsActive(database, clock, computer.SessionId, database.UserId));
        Assert.NotNull(await Refresh(database, clock, computer.RefreshToken));
    }

    /// <summary>Signing out with the token a lost answer left behind must still sign out.</summary>
    [Fact]
    public async Task SigningOut_WorksWithEitherAcceptedToken()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var held = await SignIn(database, clock);
        await Refresh(database, clock, held.RefreshToken);

        await using (var db = database.NewContext())
        {
            await Service(db, clock).EndAsync(held.RefreshToken);
        }

        await using var context = database.NewContext();
        Assert.Empty(context.LoginSessions);
    }

    /// <summary>The session id travels in the access token, so it alone must not sign anyone out.</summary>
    [Fact]
    public async Task SigningOut_WithTheSessionIdAndAWrongSecret_ChangesNothing()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);

        await using (var db = database.NewContext())
        {
            await Service(db, clock).EndAsync($"{ticket.SessionId:N}.guessed");
        }

        Assert.True(await IsActive(database, clock, ticket.SessionId, database.UserId));
    }

    [Fact]
    public async Task EndingAll_LeavesOtherAccountsSignedIn()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var other = database.AddUser();
        SetStamp(database, other, Stamp);
        var mine = await SignIn(database, clock);
        LoginSessionTicket theirs;
        await using (var db = database.NewContext())
        {
            theirs = await Service(db, clock).StartAsync(other, Stamp);
        }

        await using (var db = database.NewContext())
        {
            await Service(db, clock).EndAllAsync(database.UserId);
        }

        Assert.False(await IsActive(database, clock, mine.SessionId, database.UserId));
        Assert.True(await IsActive(database, clock, theirs.SessionId, other));
    }

    [Fact]
    public async Task ASession_IsNotActiveForAnotherAccount()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);

        Assert.True(await IsActive(database, clock, ticket.SessionId, database.UserId));
        Assert.False(await IsActive(database, clock, ticket.SessionId, database.AddUser()));
        Assert.False(await IsActive(database, clock, Guid.NewGuid(), database.UserId));
    }

    [Fact]
    public async Task SigningIn_RemovesTheExpiredSessionsOfThatAccount()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        await SignIn(database, clock);
        clock.Now = clock.Now.AddDays(20);
        var recent = await SignIn(database, clock);
        clock.Now = clock.Now.AddDays(15);

        var current = await SignIn(database, clock);

        await using var db = database.NewContext();
        Assert.Equal(
            new[] { recent.SessionId, current.SessionId }.Order(),
            db.LoginSessions.Select(session => session.Id).AsEnumerable().Order());
    }

    [Fact]
    public async Task DeletingTheAccount_DeletesItsSessions()
    {
        using var database = NewDatabase();
        await SignIn(database, new SettableClock());

        await using (var db = database.NewContext())
        {
            db.Users.Remove(db.Users.Single(user => user.Id == database.UserId));
            await db.SaveChangesAsync();
        }

        await using var context = database.NewContext();
        Assert.Empty(context.LoginSessions);
    }

    /// <summary>
    /// A second request for the same session writes between the read and the write of the first. The
    /// first must notice and start over: writing blindly would leave one of the two clients with a
    /// token the session no longer knows.
    /// </summary>
    [Fact]
    public async Task ARotationOvertakenByAnotherRequest_IsRepeatedOnTheNewState()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);
        LoginSessionTicket? overtaking = null;
        var interceptor = new BeforeFirstUpdate(async () =>
            overtaking = await Refresh(database, clock, ticket.RefreshToken));

        LoginSessionTicket? delayed;
        await using (var db = database.NewContext(interceptor))
        {
            delayed = await Service(db, clock).RefreshAsync(ticket.RefreshToken);
        }

        Assert.NotNull(overtaking);
        Assert.NotNull(delayed);
        // The delayed request wrote last, so its token is the one that goes on.
        Assert.NotNull(await Refresh(database, clock, delayed.RefreshToken));
    }

    /// <summary>The session is signed out while its refresh is on the way: the refresh must not revive it.</summary>
    [Fact]
    public async Task ARefreshOvertakenBySigningOut_Fails()
    {
        using var database = NewDatabase();
        var clock = new SettableClock();
        var ticket = await SignIn(database, clock);
        var interceptor = new BeforeFirstUpdate(async () =>
        {
            await using var other = database.NewContext();
            await Service(other, clock).EndAsync(ticket.RefreshToken);
        });

        await using var db = database.NewContext(interceptor);

        Assert.Null(await Service(db, clock).RefreshAsync(ticket.RefreshToken));
        await using var context = database.NewContext();
        Assert.Empty(context.LoginSessions);
    }

    private sealed class BeforeFirstUpdate : DbCommandInterceptor
    {
        private Func<Task>? _action;

        public BeforeFirstUpdate(Func<Task> action)
        {
            _action = action;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref _action, null) is { } action)
            {
                await action();
            }

            return result;
        }
    }
}
