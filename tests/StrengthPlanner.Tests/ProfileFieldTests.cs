using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Tests;

/// <summary>
/// Pravila za polja profila koja korisnik sam upisuje: ime i visina.
///
/// Ime i visina su dodati zbog novog toka registracije, u kome se svako polje unosi na
/// svom ekranu. Tamo je lako prevideti da wizard i server ne traže istu stvar, pa ovde
/// stoji test umesto poverenja: registracija ime traži, izmena profila ne, a visina je
/// opciona na oba mesta i ne sme da propusti vrednost koju nijedan čovek nema.
/// </summary>
public class ProfileFieldTests
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        return results;
    }

    private static RegisterDto ValidRegistration() => new()
    {
        Email = "korisnik@primer.com",
        Password = new string('x', PasswordPolicy.MinimumLength),
        DisplayName = "Mateja",
        Age = 30,
        BodyweightKg = 80,
        HeightCm = 183
    };

    private static UpdateProfileDto ValidUpdate() => new()
    {
        Age = 30,
        BodyweightKg = 80,
        HeightCm = 183
    };

    [Fact]
    public void Registration_RequiresAName()
    {
        // Ime je prvi ekran wizarda; ako ga server ne traži, dovoljno je preskočiti taj
        // ekran u zahtevu da bi nalog nastao bez imena.
        var dto = ValidRegistration();
        dto.DisplayName = string.Empty;

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.DisplayName)));
    }

    [Fact]
    public void Registration_RejectsANameLongerThanTheColumn()
    {
        // Bez granice ovde ime prolazi validaciju i pada tek na upisu, kao 500 umesto 400
        // — isti kvar koji je već izmeren sa predugom email adresom.
        var dto = ValidRegistration();
        dto.DisplayName = new string('a', ProfilePolicy.DisplayNameMaximumLength + 1);

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.DisplayName)));
    }

    [Fact]
    public void ProfileUpdate_DoesNotRequireAName()
    {
        // Nalozi napravljeni pre uvođenja imena ga nemaju. Da je i ovde obavezno, takav
        // korisnik ne bi mogao da promeni ni telesnu masu dok ne postavi ime.
        var dto = ValidUpdate();
        dto.DisplayName = null;

        Assert.DoesNotContain(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(UpdateProfileDto.DisplayName)));
    }

    [Fact]
    public void ProfileUpdate_HoldsTheNameToTheSameLengthAsRegistration()
    {
        // Inače bi izmena profila bila zaobilaznica oko granice koja važi pri registraciji.
        var dto = ValidUpdate();
        dto.DisplayName = new string('a', ProfilePolicy.DisplayNameMaximumLength + 1);

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(UpdateProfileDto.DisplayName)));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(251)]
    [InlineData(0)]
    [InlineData(-183)]
    public void Registration_RejectsAHeightNoOneHas(decimal heightCm)
    {
        var dto = ValidRegistration();
        dto.HeightCm = heightCm;

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.HeightCm)));
    }

    [Fact]
    public void Registration_AcceptsAMissingHeight()
    {
        // Visina ne ulazi ni u jedan algoritam, pa prazna vrednost ne kvari plan — isto
        // kao pol. Obavezna bi značila da nalozi stariji od polja ne mogu da se izmene.
        var dto = ValidRegistration();
        dto.HeightCm = null;

        Assert.DoesNotContain(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.HeightCm)));
    }

    [Fact]
    public void ProfileUpdate_AcceptsAMissingHeight()
    {
        var dto = ValidUpdate();
        dto.HeightCm = null;

        Assert.DoesNotContain(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(UpdateProfileDto.HeightCm)));
    }

    [Fact]
    public void HeightRange_CoversTheAdultRangeOnBothEnds()
    {
        // Granice su tu da odbiju grešku u kucanju, ne da odbiju čoveka: najniža i
        // najviša zabeležena odrasla osoba moraju da stanu unutra.
        Assert.True(ProfilePolicy.MinimumHeightCm <= 110, "Donja granica visine odbija odraslu osobu.");
        Assert.True(ProfilePolicy.MaximumHeightCm >= 240, "Gornja granica visine odbija odraslu osobu.");
    }
}
