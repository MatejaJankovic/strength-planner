using System.Text.RegularExpressions;

namespace StrengthPlanner.Tests;

/// <summary>
/// The Content-Security-Policy served by nginx must allow the way the app shows images.
///
/// The profile picture is fetched with the token and shown through a <c>blob:</c> URL. The
/// policy listed only <c>'self' data:</c>, so the browser refused it in the container deployment.
/// The development server sends no policy, which is why it was only visible behind nginx.
///
/// The test reads the files from the repository. When they are not there (published output
/// outside the working tree) it stays silent rather than failing: there is nothing to compare.
/// </summary>
public class ContentSecurityPolicyImageTests
{
    private static string? FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "strength-planner-web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void ImagePolicyAllowsBlobUrlsWhileTheAppShowsThem()
    {
        var headers = FindWebFile("security-headers.conf");
        var authService = FindWebFile("src", "app", "core", "auth", "auth.service.ts");
        if (headers is null || authService is null)
        {
            return;
        }

        Assert.Contains("URL.createObjectURL(", File.ReadAllText(authService));

        var imageSources = Regex.Match(File.ReadAllText(headers), @"Content-Security-Policy ""[^""]*?img-src ([^;""]*)");
        Assert.True(imageSources.Success, "security-headers.conf sets no img-src in its Content-Security-Policy.");
        Assert.Contains("blob:", imageSources.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
