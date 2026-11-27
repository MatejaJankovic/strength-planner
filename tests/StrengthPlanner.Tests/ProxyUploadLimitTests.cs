using System.Text.RegularExpressions;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Tests.Testing;

namespace StrengthPlanner.Tests;

/// <summary>
/// What the nginx in front of the API must do for the container deployment to work. The
/// development server does not go through nginx, so neither rule shows there.
///
/// Uploads: nginx refuses a body above 1 MB unless told otherwise, while the API accepts a
/// profile picture of up to 2 MB, so any picture between the two got nginx's own "413" page.
///
/// The API address: nginx looks a host name up once, at start. An API container recreated with
/// another address was then unreachable until nginx was restarted.
/// </summary>
public class ProxyUploadLimitTests
{
    [Fact]
    public void NginxAcceptsEveryAvatarRequestTheApiAccepts()
    {
        var config = WebProjectFiles.ReadNginxConfiguration("nginx.conf");

        // A limit inside an unrelated location would not apply to /api/, so one has to stand in
        // the server block itself, ahead of its first location.
        Assert.Matches(@"\bserver\s*\{(?:(?!\blocation\b)[^{}])*\bclient_max_body_size\s", config);

        foreach (Match limit in Regex.Matches(config, @"\bclient_max_body_size\s+(\d+)([kKmM]?)\s*;"))
        {
            long multiplier = limit.Groups[2].Value.ToLowerInvariant() switch
            {
                "k" => 1024,
                "m" => 1024 * 1024,
                _ => 1
            };
            var bytes = long.Parse(limit.Groups[1].Value) * multiplier;

            // Zero switches the check off in nginx.
            Assert.True(
                bytes == 0 || bytes >= ImageFormat.MaximumRequestSizeBytes,
                $"client_max_body_size is {bytes} bytes, below the {ImageFormat.MaximumRequestSizeBytes} the API accepts.");
        }
    }

    [Fact]
    public void NginxLooksUpTheApiAddressWhileItRuns()
    {
        var config = WebProjectFiles.ReadNginxConfiguration("nginx.conf");

        var targets = Regex.Matches(config, @"\bproxy_pass\s+([^;]+);")
            .Select(match => match.Groups[1].Value.Trim())
            .ToList();

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.Equal("http://api_backend", target));
        Assert.Matches(@"\bresolver\s+127\.0\.0\.11\b", config);
        Assert.Matches(@"\bupstream\s+api_backend\s*\{[^}]*\bserver\s+api:8080\s[^;}]*\bresolve\b", config);
    }
}
