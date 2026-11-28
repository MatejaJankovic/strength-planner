using System.Text.RegularExpressions;
using StrengthPlanner.Tests.Testing;

namespace StrengthPlanner.Tests;

/// <summary>
/// The app loads nothing from other people's servers: a font or a script fetched from elsewhere
/// hands that server the visitor's IP address, and the privacy policy says this does not happen.
/// </summary>
public class NoThirdPartyResourcesTests
{
    private static readonly string[] LocalSources = { "'self'", "'none'", "'unsafe-inline'", "data:", "blob:" };

    [Fact]
    public void ContentSecurityPolicy_NamesNoOtherOrigin()
    {
        var configuration = WebProjectFiles.ReadNginxConfiguration("security-headers.conf");
        var policy = Regex.Match(configuration, "Content-Security-Policy \"(?<policy>[^\"]+)\"");
        Assert.True(policy.Success, "The Content-Security-Policy header was not found.");

        var foreignSources = policy.Groups["policy"].Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // The first word of a directive is its name.
            .SelectMany(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            .Where(source => !LocalSources.Contains(source));

        Assert.Empty(foreignSources);
    }

    [Fact]
    public void IndexPage_LoadsNothingFromAnotherServer()
    {
        var page = Regex.Replace(
            File.ReadAllText(WebProjectFiles.Find("src", "index.html")),
            "<!--.*?-->",
            string.Empty,
            RegexOptions.Singleline);

        Assert.DoesNotMatch("(?i)(href|src)\\s*=\\s*[\"'](https?:)?//", page);
    }

    [Fact]
    public void Stylesheets_LoadNothingFromAnotherServer()
    {
        var sources = Path.Combine(Path.GetDirectoryName(WebProjectFiles.Find("angular.json"))!, "src");
        var stylesheets = Directory.GetFiles(sources, "*.scss", SearchOption.AllDirectories);
        Assert.NotEmpty(stylesheets);

        var external = new Regex("(?i)(url\\(\\s*|@(import|use)\\s+)[\"']?(https?:)?//[\\w-]+\\.");

        Assert.Empty(stylesheets.Where(stylesheet => external.IsMatch(File.ReadAllText(stylesheet))));
    }
}
