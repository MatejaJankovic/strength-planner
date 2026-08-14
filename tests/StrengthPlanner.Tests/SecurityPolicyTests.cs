using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.DTOs.Exercises;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Bezbednosna pravila koja postoje na više mesta odjednom. Svako od njih je već jednom
/// bilo razmimoiđeno ili prelabavo, pa ih ovde drži test umesto komentara.
/// </summary>
public class SecurityPolicyTests
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        return results;
    }

    private static RegisterDto ValidRegistration(string password) => new()
    {
        Email = "korisnik@primer.com",
        Password = password,
        Age = 30,
        BodyweightKg = 80,
        TrainingDaysPerWeek = 3
    };

    [Fact]
    public void PasswordMinimum_IsLongEnoughToBeWorthSomething()
    {
        // Bilo je šest, što je prihvatalo "123456". Dužina je jedina mera koja stvarno
        // otežava pogađanje, pa spuštanje ove vrednosti mora da bude svesna odluka.
        Assert.True(
            PasswordPolicy.MinimumLength >= 10,
            $"Najmanja dužina lozinke je spuštena na {PasswordPolicy.MinimumLength}.");
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("aaaaaa")]
    [InlineData("Test1234!")]
    public void Registration_RejectsShortPasswords(string password)
    {
        Assert.Contains(
            Validate(ValidRegistration(password)),
            result => result.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void Registration_AcceptsAPasswordOfTheRequiredLength()
    {
        var password = new string('x', PasswordPolicy.MinimumLength);

        Assert.DoesNotContain(
            Validate(ValidRegistration(password)),
            result => result.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void ChangePassword_HoldsTheNewPasswordToTheSamePolicy()
    {
        // Inače bi promena lozinke bila zaobilaznica oko pravila koje važi pri registraciji.
        var dto = new ChangePasswordDto { CurrentPassword = "svejedno", NewPassword = "kratka" };

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(ChangePasswordDto.NewPassword)));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    [InlineData(0x7FFFFFFF)]
    public void Registration_RejectsAnExperienceLevelThatDoesNotExist(int level)
    {
        // Model binder prima bilo koji ceo broj za enum. Bez provere je "experienceLevel":
        // 999 vraćalo 200, upisivalo se u profil i vraćalo klijentu kao nivo koji nijedan
        // ekran ne prikazuje, dok bi algoritmi na njega odgovarali podrazumevanom granom.
        var dto = ValidRegistration(new string('x', PasswordPolicy.MinimumLength));
        dto.ExperienceLevel = (ExperienceLevel)level;

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.ExperienceLevel)));
    }

    [Fact]
    public void Registration_AcceptsEveryExperienceLevelTheSystemDefines()
    {
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var dto = ValidRegistration(new string('x', PasswordPolicy.MinimumLength));
            dto.ExperienceLevel = level;

            Assert.DoesNotContain(
                Validate(dto),
                result => result.MemberNames.Contains(nameof(RegisterDto.ExperienceLevel)));
        }
    }

    [Fact]
    public void ProfileUpdate_RejectsAnExperienceLevelThatDoesNotExist()
    {
        // Ista provera i ovde: inače je izmena profila zaobilaznica oko registracije.
        var dto = new UpdateProfileDto
        {
            Age = 30,
            BodyweightKg = 80,
            TrainingDaysPerWeek = 3,
            ExperienceLevel = (ExperienceLevel)999
        };

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(UpdateProfileDto.ExperienceLevel)));
    }

    [Fact]
    public void Registration_RejectsAnEmailLongerThanTheColumnThatStoresIt()
    {
        // Granica prati Identity kolonu. Bez nje je adresa od 400 znakova prolazila
        // validaciju i padala tek pri upisu — izmereno, vraćala je 500 umesto 400.
        var dto = ValidRegistration(new string('x', PasswordPolicy.MinimumLength));
        dto.Email = new string('a', EmailPolicy.MaximumLength) + "@primer.com";

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.Email)));
    }

    [Fact]
    public void Registration_RejectsAPasswordLongerThanThePolicyAllows()
    {
        var dto = ValidRegistration(new string('x', PasswordPolicy.MaximumLength + 1));

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void Login_HoldsEmailAndPasswordToTheSameBounds()
    {
        // Prijava prima ista polja i troši isti posao na njima; bez granica bi bila put
        // oko onih postavljenih na registraciji.
        var dto = new LoginDto
        {
            Email = new string('a', EmailPolicy.MaximumLength) + "@primer.com",
            Password = new string('x', PasswordPolicy.MaximumLength + 1)
        };

        var results = Validate(dto);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(LoginDto.Email)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(LoginDto.Password)));
    }

    [Fact]
    public void PasswordBounds_LeaveRoomForARealPassword()
    {
        // Da se donja i gornja granica ne bi jednog dana ukrstile.
        Assert.True(PasswordPolicy.MaximumLength > PasswordPolicy.MinimumLength);
    }

    [Fact]
    public void MuscleGroupCap_MatchesTheNumberOfGroupsThatActuallyExist()
    {
        // Granica mora da bude const zbog atributa validacije, pa ne može da se izvede iz
        // kataloga. Dodavanje jedanaeste grupe bez ove provere tiho odbija ispravne zahteve.
        Assert.Equal(ExerciseCatalog.MuscleGroupNames.Count, CreateExerciseRequest.MaxMuscleGroups);
    }

    [Fact]
    public void CustomExercise_RejectsMoreMuscleGroupsThanExist()
    {
        var request = new CreateExerciseRequest
        {
            Name = "Vežba",
            Type = "Isolation",
            Equipment = "Cable",
            Muscles = Enumerable
                .Range(0, CreateExerciseRequest.MaxMuscleGroups + 1)
                .Select(_ => new MuscleContributionDto { MuscleGroup = "Chest", Contribution = 1.0m })
                .ToList()
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(CreateExerciseRequest.Muscles)));
    }

    [Fact]
    public void CustomExercise_EquipmentCannotExceedTheDatabaseColumn()
    {
        // Kolona je varchar(32); duži tekst je ranije prolazio validaciju i padao kao 500.
        var request = new CreateExerciseRequest
        {
            Name = "Vežba",
            Type = "Isolation",
            Equipment = new string('X', 33),
            Muscles = [new MuscleContributionDto { MuscleGroup = "Chest", Contribution = 1.0m }]
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(CreateExerciseRequest.Equipment)));
    }
}
