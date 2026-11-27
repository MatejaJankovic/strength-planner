using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StrengthPlanner.API.Controllers;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.Tests;

/// <summary>
/// What the client relies on from the two session endpoints. Both are called exactly when the access
/// token is useless, so losing <c>[AllowAnonymous]</c> would sign every lifter out after an hour; and
/// the client ends a sign-in only on 401.
/// </summary>
public class SessionEndpointTests
{
    [Theory]
    [InlineData(nameof(AuthController.Refresh), "refresh")]
    [InlineData(nameof(AuthController.Logout), "logout")]
    public void TheSessionEndpoints_AreAnonymousAndRateLimited(string action, string route)
    {
        var method = typeof(AuthController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("session", method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Equal(route, method.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }

    [Fact]
    public async Task ARefusedRefresh_Answers401()
    {
        var controller = new AuthController(new RefusingAuthService());

        var answer = await controller.Refresh(new RefreshTokenDto { RefreshToken = "x" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(answer);
    }

    /// <summary>Only the refresh is answered; the other members are not reached by these tests.</summary>
    private sealed class RefusingAuthService : IAuthService
    {
        public Task<AuthResponseDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthResponseDto?>(null);

        public Task<AuthResponseDto> RegisterAsync(RegisterDto dto) => throw new NotSupportedException();

        public Task<AuthResponseDto> LoginAsync(LoginDto dto) => throw new NotSupportedException();

        public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId) => throw new NotSupportedException();

        public Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
            => throw new NotSupportedException();

        public Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
            => throw new NotSupportedException();

        public Task<CurrentUserDto> SetAvatarAsync(Guid userId, byte[] content) => throw new NotSupportedException();

        public Task<AvatarDto?> GetAvatarAsync(Guid userId) => throw new NotSupportedException();

        public Task<CurrentUserDto> RemoveAvatarAsync(Guid userId) => throw new NotSupportedException();

        public Task DeleteAccountAsync(Guid userId, DeleteAccountDto dto) => throw new NotSupportedException();
    }
}
