using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.API.Security;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// The whole path of an account: <see cref="AuthService"/> with the real Identity user manager, the
/// real token service and the check every request goes through. A sign-in rule that is right in its
/// own class and never called would pass <c>LoginSessionServiceTests</c>.
/// </summary>
public class SignedInAccountTests : IDisposable
{
    private const string Password = "a-password-for-this-test";
    private const string NewPassword = "another-password-for-this-test";

    private readonly SqliteDatabase _database = new();
    private readonly ServiceProvider _provider;

    public SignedInAccountTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _database.NewContext());
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.Configure<JwtSettings>(settings =>
        {
            settings.Issuer = "tests";
            settings.Audience = "tests";
            settings.Key = new string('k', 48);
        });
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ILoginSessionService, LoginSessionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<AccessTokenCheck>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _database.Dispose();
    }

    /// <summary>One request: its own scope and its own context, as in the running application.</summary>
    private async Task<T> InRequest<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private Task<AuthResponseDto> Register() => InRequest(services =>
        services.GetRequiredService<IAuthService>().RegisterAsync(new RegisterDto
        {
            Email = "lifter@example.test",
            Password = Password,
            DisplayName = "Lifter",
            Age = 30,
            BodyweightKg = 80,
            ExperienceLevel = ExperienceLevel.Intermediate
        }));

    private Task<AuthResponseDto> Login(string password) => InRequest(services =>
        services.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginDto { Email = "lifter@example.test", Password = password }));

    private Task<AuthResponseDto?> Refresh(string refreshToken) => InRequest(services =>
        services.GetRequiredService<IAuthService>().RefreshAsync(refreshToken));

    private Task<bool> IsAccepted(string accessToken) => InRequest(async services =>
    {
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return await services.GetRequiredService<AccessTokenCheck>()
            .RefusalAsync(principal, CancellationToken.None) is null;
    });

    [Fact]
    public async Task RegisteringAndSigningIn_EachOpenASessionTheAccessTokenNames()
    {
        var registered = await Register();
        var loggedIn = await Login(Password);

        Assert.True(await IsAccepted(registered.Token));
        Assert.True(await IsAccepted(loggedIn.Token));
        Assert.NotEmpty(registered.RefreshToken);
        Assert.NotEqual(SessionOf(registered.Token), SessionOf(loggedIn.Token));
    }

    [Fact]
    public async Task ARefresh_GivesAnAccessTokenOfTheSameSession()
    {
        var registered = await Register();

        var refreshed = await Refresh(registered.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.Equal(registered.UserId, refreshed.UserId);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(SessionOf(registered.Token), SessionOf(refreshed.Token));
        Assert.True(await IsAccepted(refreshed.Token));
    }

    [Fact]
    public async Task SigningOut_RefusesTheAccessTokenAtOnce_AndLeavesTheOtherDevice()
    {
        var phone = await Register();
        var computer = await Login(Password);

        await InRequest(async services =>
        {
            await services.GetRequiredService<IAuthService>().LogoutAsync(phone.RefreshToken);
            return true;
        });

        Assert.False(await IsAccepted(phone.Token));
        Assert.Null(await Refresh(phone.RefreshToken));
        Assert.True(await IsAccepted(computer.Token));
    }

    [Fact]
    public async Task ChangingThePassword_SignsOutEveryDeviceAndKeepsThisOne()
    {
        var phone = await Register();
        var computer = await Login(Password);

        var changed = await InRequest(services => services.GetRequiredService<IAuthService>()
            .ChangePasswordAsync(
                phone.UserId,
                new ChangePasswordDto { CurrentPassword = Password, NewPassword = NewPassword }));

        Assert.False(await IsAccepted(phone.Token));
        Assert.False(await IsAccepted(computer.Token));
        Assert.Null(await Refresh(computer.RefreshToken));
        Assert.True(await IsAccepted(changed.Token));
        Assert.NotNull(await Refresh(changed.RefreshToken));
        await using var db = _database.NewContext();
        Assert.Single(db.LoginSessions);
    }

    /// <summary>
    /// Five wrong passwords lock the account against further guessing. Signing out whoever is already
    /// in would let anyone who knows the address throw its owner out.
    /// </summary>
    [Fact]
    public async Task ALockedAccount_StaysSignedInWhereItAlreadyIs()
    {
        var registered = await Register();
        await InRequest(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(registered.UserId.ToString());
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(5));
            return true;
        });

        Assert.NotNull(await Refresh(registered.RefreshToken));
        Assert.True(await IsAccepted(registered.Token));
    }

    /// <summary>Tokens issued before sessions existed name none, and are refused.</summary>
    [Fact]
    public async Task AnAccessTokenWithoutASession_IsRefused()
    {
        var registered = await Register();
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(registered.Token).Claims
            .Where(claim => claim.Type != JwtTokenService.SessionIdClaim);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var refusal = await InRequest(services => services.GetRequiredService<AccessTokenCheck>()
            .RefusalAsync(principal, CancellationToken.None));

        Assert.NotNull(refusal);
    }

    private static string SessionOf(string accessToken)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .Single(claim => claim.Type == JwtTokenService.SessionIdClaim)
            .Value;
    }
}
