using StrengthPlanner.Application.DTOs.Auth;

namespace StrengthPlanner.Tests;

/// <summary>
/// Zamka za automate je jedna provera u <c>AuthService.RegisterAsync</c>, koja se briše ili
/// obrne jednim potezom i ništa ne pukne — registracija i dalje radi za sve. Ovde je pravilo
/// zapisano da bi takva izmena morala da bude svesna.
///
/// Sama provera se ovde ne izvršava (za to bi trebao UserManager i baza); proverava se
/// odluka koju ona donosi, na istim vrednostima na kojima je donosi i servis.
/// </summary>
public class HoneypotTests
{
    /// <summary>
    /// Isti izraz koji <c>AuthService.RegisterAsync</c> koristi da prepozna automat.
    /// Ako se tamo promeni, ovde mora da padne.
    /// </summary>
    private static bool LooksLikeABot(RegisterDto dto) => !string.IsNullOrWhiteSpace(dto.Website);

    private static RegisterDto Registration(string? website) => new()
    {
        Email = "korisnik@primer.com",
        Password = "dovoljno-duga-lozinka",
        Age = 30,
        BodyweightKg = 80,
        TrainingDaysPerWeek = 3,
        Website = website
    };

    [Theory]
    [InlineData("http://spam.example")]
    [InlineData("bilo šta")]
    [InlineData("0")]
    public void AFilledHoneypot_IsTreatedAsABot(string website)
    {
        Assert.True(LooksLikeABot(Registration(website)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // Prazan string i sam razmak moraju da prođu: Angular za nedirnuto polje šalje prazan
    // string, a ne null, pa bi provera na samo `!= null` odbila svaku pravu registraciju.
    [InlineData("   ")]
    public void AnUntouchedHoneypot_LetsARealPersonThrough(string? website)
    {
        Assert.False(LooksLikeABot(Registration(website)));
    }
}
