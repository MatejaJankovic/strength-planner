using System.Text.RegularExpressions;
using StrengthPlanner.Tests.Testing;

namespace StrengthPlanner.Tests;

/// <summary>
/// The Content-Security-Policy served by nginx must allow the way the app shows images.
///
/// The profile picture is fetched with the token and shown through a <c>blob:</c> URL. The
/// policy listed only <c>'self' data:</c>, so the browser refused it in the container deployment.
/// The development server sends no policy, which is why it was only visible behind nginx.
/// </summary>
public class ContentSecurityPolicyImageTests
{
    [Fact]
    public void EveryImagePolicyAllowsBlobUrls()
    {
        var headers = WebProjectFiles.ReadNginxConfiguration("security-headers.conf");

        var policies = Regex.Matches(headers, @"Content-Security-Policy\s+""([^""]*)""");
        Assert.NotEmpty(policies);

        foreach (Match policy in policies)
        {
            var imageSources = Regex.Match(policy.Groups[1].Value, @"(?:^|;)\s*img-src\s+([^;]*)");
            Assert.True(imageSources.Success, "A Content-Security-Policy in security-headers.conf sets no img-src.");
            Assert.Contains("blob:", imageSources.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
