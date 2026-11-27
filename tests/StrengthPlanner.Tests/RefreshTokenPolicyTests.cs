using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Tests;

public class RefreshTokenPolicyTests
{
    [Fact]
    public void AnIssuedToken_IsReadBackAsItsSessionAndHash()
    {
        var session = Guid.NewGuid();
        var (token, hash) = RefreshTokenPolicy.Issue(session);

        Assert.True(RefreshTokenPolicy.TryRead(token, out var readSession, out var readHash));
        Assert.Equal(session, readSession);
        Assert.Equal(hash, readHash);
    }

    /// <summary>The database keeps the hash, so the hash must not contain what signs a lifter in.</summary>
    [Fact]
    public void TheStoredHash_DoesNotCarryTheSecret()
    {
        var (token, hash) = RefreshTokenPolicy.Issue(Guid.NewGuid());
        var secret = token[(token.IndexOf('.') + 1)..];

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(secret, hash, StringComparison.OrdinalIgnoreCase);
        // 256 random bits written in base64 without padding.
        Assert.Equal(43, secret.Length);
        Assert.True(token.Length <= RefreshTokenPolicy.MaximumTokenLength);
    }

    [Fact]
    public void TwoTokensOfOneSession_Differ()
    {
        var session = Guid.NewGuid();

        Assert.NotEqual(RefreshTokenPolicy.Issue(session).Token, RefreshTokenPolicy.Issue(session).Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData(".secret-without-session")]
    [InlineData("0123456789abcdef0123456789abcdef.")]
    [InlineData("not-a-guid.secret")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef.secret")]
    public void AnythingElse_IsNotAToken(string? token)
    {
        Assert.False(RefreshTokenPolicy.TryRead(token, out _, out _));
    }

    [Fact]
    public void AnOverlongToken_IsRefusedBeforeItIsHashed()
    {
        var token = $"{Guid.NewGuid():N}.{new string('a', RefreshTokenPolicy.MaximumTokenLength)}";

        Assert.False(RefreshTokenPolicy.TryRead(token, out _, out _));
    }

    [Theory]
    [InlineData("issued", "held", "issued", RefreshTokenUse.Newest)]
    [InlineData("held", "held", "issued", RefreshTokenUse.Retried)]
    [InlineData("held", "held", null, RefreshTokenUse.Retried)]
    [InlineData("older", "held", "issued", RefreshTokenUse.Reused)]
    [InlineData("issued", "held", null, RefreshTokenUse.Reused)]
    public void APresentedToken_IsPlacedAmongTheTwoTheSessionAccepts(
        string presented, string held, string? issued, RefreshTokenUse expected)
    {
        Assert.Equal(expected, RefreshTokenPolicy.Classify(presented, held, issued));
    }
}
