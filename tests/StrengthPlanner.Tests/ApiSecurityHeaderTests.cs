using StrengthPlanner.API.Security;

namespace StrengthPlanner.Tests;

/// <summary>
/// Skup bezbednosnih zaglavlja koja API šalje. Lista je statična i briše se jednim potezom,
/// a nedostatak se ne vidi ni u jednom testu ni na ekranu — odgovor izgleda isto.
///
/// Ovde stoji šta mora da bude poslato, ne kako je sastavljeno.
/// </summary>
public class ApiSecurityHeaderTests
{
    [Theory]
    [InlineData("Content-Security-Policy")]
    [InlineData("X-Content-Type-Options")]
    [InlineData("X-Frame-Options")]
    [InlineData("Referrer-Policy")]
    [InlineData("Permissions-Policy")]
    // Odgovori nose lične trenažne podatke i nemaju šta da traže u deljenom kešu.
    [InlineData("Cache-Control")]
    public void TheApiSends(string header)
    {
        Assert.Contains(SecurityHeaders.All, entry => entry.Name == header);
    }

    [Fact]
    public void TheContentPolicy_DeniesEverythingByDefault()
    {
        // API vraća isključivo JSON. Ako se takav odgovor ipak negde prikaže kao dokument,
        // ne sme da ima šta da izvrši — pa svako popuštanje ovog pravila mora da bude
        // svesna odluka, a ne posledica kopiranja pravila sa frontenda.
        var policy = SecurityHeaders.All.Single(entry => entry.Name == "Content-Security-Policy").Value;

        Assert.Contains("default-src 'none'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.DoesNotContain("unsafe-inline", policy);
    }
}
