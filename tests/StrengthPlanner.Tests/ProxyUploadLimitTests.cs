using System.Text.RegularExpressions;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Tests;

/// <summary>
/// The nginx in front of the API must let through every upload the API accepts.
///
/// nginx refuses a body above 1 MB unless told otherwise, while the API accepts a profile
/// picture of up to 2 MB. The development server does not go through nginx, so the gap only
/// showed in the container deployment, as nginx's own "413" page for any picture between the
/// two limits.
///
/// The test reads the configuration from the repository. When the file is not there (published
/// output outside the working tree) it stays silent rather than failing: there is nothing to compare.
/// </summary>
public class ProxyUploadLimitTests
{
    private static string? FindNginxConfig()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "strength-planner-web", "nginx.conf");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void NginxAcceptsEveryAvatarRequestTheApiAccepts()
    {
        var path = FindNginxConfig();
        if (path is null)
        {
            return;
        }

        // Comments mention the directive by name, so they are removed before reading it.
        var config = Regex.Replace(File.ReadAllText(path), "#[^\n]*", string.Empty);

        var limits = Regex.Matches(config, @"\bclient_max_body_size\s+(\d+)([kKmM]?)\s*;");
        Assert.True(limits.Count > 0, "nginx.conf does not set client_max_body_size, so nginx stops at 1 MB.");

        foreach (Match limit in limits)
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
}
