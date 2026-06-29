namespace StrengthPlanner.Infrastructure.Authentication;

/// <summary>
/// JWT postavke (vezuju se na "Jwt" sekciju u appsettings).
/// Key je dev-only placeholder — u produkciji ide u tajne/environment.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
