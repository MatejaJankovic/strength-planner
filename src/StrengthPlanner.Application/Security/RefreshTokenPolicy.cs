using System.Security.Cryptography;
using System.Text;

namespace StrengthPlanner.Application.Security;

/// <summary>What to do with a refresh token a client presented for a session that is still alive.</summary>
public enum RefreshTokenUse
{
    /// <summary>The token issued last: the client received it, so it becomes the one it is known to hold.</summary>
    Newest,

    /// <summary>
    /// The token the client is known to hold, presented again: the answer carrying its successor was
    /// lost, or two requests left at once. The successor is replaced.
    /// </summary>
    Retried,

    /// <summary>
    /// Neither of the two. A token that was already replaced is in someone else's hands as well, so
    /// the session is ended.
    /// </summary>
    Reused
}

/// <summary>
/// Rules of the refresh token, which keeps a lifter signed in between workouts.
///
/// A token is <c>{session id}.{secret}.{mac}</c>. The session id finds the row, and only a hash of the
/// secret is stored, so a copy of the database signs nobody in. The MAC, made with a key only the
/// server has, tells a token this application issued from a string that merely names a session: the id
/// is also in every access token, and knowing it must not be enough to end the session. It also lets a
/// replaced token be recognised as ours without keeping every one of them.
///
/// Each use hands out a new token. The old one is not dropped at that moment, because a phone in a
/// gym loses answers: the server would rotate, the client would never learn the new token, and the
/// next attempt would look like theft. So a session remembers two hashes, the token the client is
/// known to hold and the one issued after it. The first stays usable until the second shows up.
/// A copied token still gets caught: once both holders have used the session, one of them presents a
/// token that is neither, whichever of them went first.
/// </summary>
public static class RefreshTokenPolicy
{
    /// <summary>How long a session lasts without being refreshed. Every refresh starts it again.</summary>
    public const int DefaultIdleDays = 30;

    /// <summary>
    /// Longest accepted token. An issued one has 120 characters: 32 of id, 43 of secret, 43 of MAC
    /// and two dots.
    /// </summary>
    public const int MaximumTokenLength = 128;

    private const int SecretSizeBytes = 32;

    /// <summary>A new token for the session, and the hash of it that is stored.</summary>
    public static (string Token, string Hash) Issue(Guid sessionId, byte[] macKey)
    {
        var secret = Encode(RandomNumberGenerator.GetBytes(SecretSizeBytes));
        var body = $"{sessionId:N}.{secret}";

        return ($"{body}.{Mac(body, macKey)}", Hash(secret));
    }

    /// <summary>
    /// Splits a presented token into the session it names and the hash of its secret. False for
    /// anything this application did not issue with this key.
    /// </summary>
    public static bool TryRead(string? token, byte[] macKey, out Guid sessionId, out string secretHash)
    {
        sessionId = Guid.Empty;
        secretHash = string.Empty;

        if (string.IsNullOrEmpty(token) || token.Length > MaximumTokenLength)
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out var id)
            || !Matches(Mac($"{parts[0]}.{parts[1]}", macKey), parts[2]))
        {
            return false;
        }

        sessionId = id;
        secretHash = Hash(parts[1]);
        return true;
    }

    /// <summary>Where a presented token stands among the two the session accepts.</summary>
    public static RefreshTokenUse Classify(string presentedHash, string heldHash, string? issuedHash)
    {
        if (issuedHash is not null && Matches(presentedHash, issuedHash))
        {
            return RefreshTokenUse.Newest;
        }

        return Matches(presentedHash, heldHash) ? RefreshTokenUse.Retried : RefreshTokenUse.Reused;
    }

    private static string Hash(string secret)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    private static string Mac(string body, byte[] key)
    {
        return Encode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body)));
    }

    /// <summary>Base64 safe in a URL and without padding.</summary>
    private static string Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool Matches(string first, string second)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(first),
            Encoding.UTF8.GetBytes(second));
    }
}
