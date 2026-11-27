using System.Text.RegularExpressions;

namespace StrengthPlanner.Tests.Testing;

/// <summary>
/// Reads configuration files of the web project, for tests that hold them in agreement with the code.
///
/// A missing file fails the test. These checks guard the deployment, and one that stays silent
/// when its file is moved or renamed would keep passing while the defect it guards against returns.
/// </summary>
internal static class WebProjectFiles
{
    /// <summary>Text of an nginx configuration file with its comments removed.</summary>
    public static string ReadNginxConfiguration(string fileName)
    {
        // Comments mention directives by name, and a commented-out line must not count as one.
        return Regex.Replace(File.ReadAllText(Find(fileName)), @"(?m)^\s*#.*$", string.Empty);
    }

    /// <summary>Full path of a file under <c>strength-planner-web</c>.</summary>
    public static string Find(params string[] parts)
    {
        var relative = Path.Combine(new[] { "strength-planner-web" }.Concat(parts).ToArray());
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"'{relative}' was not found above {AppContext.BaseDirectory}.");
    }
}
