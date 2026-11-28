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

    /// <summary>
    /// The page, the component templates and the stylesheets. An ordinary link (<c>&lt;a href&gt;</c>) is not
    /// listed: it loads nothing until the visitor follows it. An address put together in code is not
    /// seen here; the content security policy refuses it in the deployed app.
    /// </summary>
    [Fact]
    public void Sources_LoadNothingFromAnotherServer()
    {
        var sources = Path.Combine(Path.GetDirectoryName(WebProjectFiles.Find("angular.json"))!, "src");
        var files = Directory.GetFiles(sources, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file) is ".html" or ".scss" or ".css" or ".ts")
            .Where(file => !file.EndsWith(".spec.ts", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(files, file => file.EndsWith("index.html", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith(".scss", StringComparison.Ordinal));

        var offenders = files.Where(file => LoadsFromElsewhere(File.ReadAllText(file)));

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css\">")]
    [InlineData("<link href=//fonts.googleapis.com/css rel=stylesheet>")]
    [InlineData("<script src='https://cdn.example.com/a.js'></script>")]
    [InlineData("<img srcset=\"https://cdn.example.com/a.png 2x\">")]
    [InlineData("<iframe src=https://example.com></iframe>")]
    [InlineData("<object data=\"https://example.com/a.swf\"></object>")]
    [InlineData("<style>@import url(https://fonts.googleapis.com/css2?family=Inter);</style>")]
    [InlineData("@font-face { src: url('https://fonts.gstatic.com/s/a.woff2'); }")]
    [InlineData("@import '//fonts.googleapis.com/css';")]
    public void TheSourceCheck_SeesALoadFromAnotherServer(string source)
    {
        Assert.True(LoadsFromElsewhere(source));
    }

    [Theory]
    [InlineData("<a href=\"https://www.poverenik.rs\">poverenik.rs</a>")]
    [InlineData("<link rel=\"manifest\" href=\"manifest.webmanifest\">")]
    [InlineData("<img src=\"/logo.png\">")]
    [InlineData("// komentar o https://example.com")]
    [InlineData("const apiUrl = 'http://localhost:5241/api';")]
    [InlineData("<!-- <script src=\"https://example.com/a.js\"></script> -->")]
    public void TheSourceCheck_LeavesLocalFilesAndPlainLinksAlone(string source)
    {
        Assert.False(LoadsFromElsewhere(source));
    }

    /// <summary>The fonts go to every visitor, so their licence texts go into the image with them.</summary>
    [Fact]
    public void TheWebImage_CarriesTheLicenceTexts()
    {
        var dockerfile = File.ReadAllText(WebProjectFiles.Find("Dockerfile"));

        Assert.Matches(
            @"(?m)^COPY --from=build /app/dist/strength-planner-web/3rdpartylicenses\.txt /usr/share/nginx/html/",
            dockerfile);
    }

    private static bool LoadsFromElsewhere(string source)
    {
        var withoutComments = Regex.Replace(source, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        return Regex.IsMatch(
            withoutComments,
            // An element that fetches what it names, then the two ways a stylesheet does.
            "(?is)<(link|script|img|iframe|source|video|audio|embed|object|track|input)\\b[^>]*?"
            + "\\s(href|src|srcset|data|poster)\\s*=\\s*[\"']?\\s*(https?:)?//"
            + "|(url\\(|@(import|use))\\s*[\"']?\\s*(https?:)?//[\\w-]+\\.");
    }
}
