using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Tests;

public class RefreshTokenPolicyTests
{
    private static readonly byte[] Key = Enumerable.Repeat((byte)7, 32).ToArray();

    [Fact]
    public void AnIssuedToken_IsReadBackAsItsSessionAndHash()
    {
        var session = Guid.NewGuid();
        var (token, hash) = RefreshTokenPolicy.Issue(session, Key);

        Assert.True(RefreshTokenPolicy.TryRead(token, Key, out var readSession, out var readHash));
        Assert.Equal(session, readSession);
        Assert.Equal(hash, readHash);
    }

    /// <summary>The database keeps the hash, so the hash must not contain what signs a lifter in.</summary>
    [Fact]
    public void TheStoredHash_DoesNotCarryTheSecret()
    {
        var (token, hash) = RefreshTokenPolicy.Issue(Guid.NewGuid(), Key);
        var secret = token.Split('.')[1];

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(secret, hash, StringComparison.OrdinalIgnoreCase);
        // 256 random bits written in base64 without padding.
        Assert.Equal(43, secret.Length);
        Assert.Equal(120, token.Length);
        Assert.True(token.Length <= RefreshTokenPolicy.MaximumTokenLength);
    }

    [Fact]
    public void TwoTokensOfOneSession_Differ()
    {
        var session = Guid.NewGuid();

        Assert.NotEqual(
            RefreshTokenPolicy.Issue(session, Key).Token,
            RefreshTokenPolicy.Issue(session, Key).Token);
    }

    /// <summary>
    /// The session id is public to whoever sees an access token. Only a string made with the server's
    /// key may count as that session's token, current or replaced.
    /// </summary>
    [Fact]
    public void OnlyATokenMadeWithTheServerKey_IsRead()
    {
        var session = Guid.NewGuid();
        var (token, _) = RefreshTokenPolicy.Issue(session, Key);
        var parts = token.Split('.');
        var otherKey = Enumerable.Repeat((byte)8, 32).ToArray();
        var other = RefreshTokenPolicy.Issue(Guid.NewGuid(), Key).Token.Split('.');

        Assert.False(RefreshTokenPolicy.TryRead(token, otherKey, out var read, out _));
        Assert.Equal(Guid.Empty, read);
        // Another secret under the same MAC, and a genuine secret and MAC moved to this session.
        Assert.False(RefreshTokenPolicy.TryRead($"{parts[0]}.{other[1]}.{parts[2]}", Key, out _, out _));
        Assert.False(RefreshTokenPolicy.TryRead($"{parts[0]}.{other[1]}.{other[2]}", Key, out _, out _));
        Assert.False(RefreshTokenPolicy.TryRead($"{parts[0]}.{parts[1]}", Key, out _, out _));
        Assert.False(RefreshTokenPolicy.TryRead($"{token}.more", Key, out _, out _));
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
        Assert.False(RefreshTokenPolicy.TryRead(token, Key, out _, out _));
    }

    [Fact]
    public void AnOverlongToken_IsRefusedBeforeItIsHashed()
    {
        var token = $"{Guid.NewGuid():N}.{new string('a', RefreshTokenPolicy.MaximumTokenLength)}.mac";

        Assert.False(RefreshTokenPolicy.TryRead(token, Key, out _, out _));
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
