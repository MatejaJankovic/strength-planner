using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using StrengthPlanner.API.Controllers;
using StrengthPlanner.API.Security;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Tests.Testing;

namespace StrengthPlanner.Tests;

/// <summary>
/// The promises of the privacy notice that code has to keep: an account is made only with explicit
/// consent and only from the stated age, and the notice can be read before there is an account.
/// </summary>
public class PrivacyNoticeTests
{
    private static RegisterDto Registration() => new()
    {
        Email = "lifter@example.test",
        Password = new string('x', PasswordPolicy.MinimumLength),
        DisplayName = "Lifter",
        Age = 30,
        BodyweightKg = 80,
        ExperienceLevel = ExperienceLevel.Intermediate,
        PrivacyConsent = true
    };

    private static IEnumerable<string> RefusedFields(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames);
    }

    [Fact]
    public void Registration_WithConsent_IsAccepted()
    {
        Assert.Empty(RefusedFields(Registration()));
    }

    [Fact]
    public void Registration_WithoutConsent_IsRefused()
    {
        var registration = Registration();
        registration.PrivacyConsent = false;

        Assert.Equal(new[] { nameof(RegisterDto.PrivacyConsent) }, RefusedFields(registration));
    }

    /// <summary>A client that never sends the field has not consented to anything.</summary>
    [Fact]
    public void Registration_ThatLeavesConsentOut_IsRefused()
    {
        var body = $$"""
            {"email":"lifter@example.test","password":"{{new string('x', PasswordPolicy.MinimumLength)}}",
             "displayName":"Lifter","age":30,"bodyweightKg":80,"experienceLevel":1}
            """;

        var registration = JsonSerializer.Deserialize<RegisterDto>(
            body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(new[] { nameof(RegisterDto.PrivacyConsent) }, RefusedFields(registration));
    }

    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void AnAccount_StartsAtSixteen(int age, bool accepted)
    {
        var registration = Registration();
        registration.Age = age;
        var profile = new UpdateProfileDto
        {
            Age = age,
            BodyweightKg = 80,
            ExperienceLevel = ExperienceLevel.Intermediate
        };

        Assert.Equal(accepted, !RefusedFields(registration).Contains(nameof(RegisterDto.Age)));
        Assert.Equal(accepted, !RefusedFields(profile).Contains(nameof(UpdateProfileDto.Age)));
    }

    /// <summary>The client refuses the same ages, so a field is marked instead of a bare 400.</summary>
    [Theory]
    [InlineData("AGE_MIN", ProfilePolicy.MinimumAge)]
    [InlineData("AGE_MAX", ProfilePolicy.MaximumAge)]
    public void TheClient_KeepsTheSameAgeLimits(string constant, int expected)
    {
        var models = File.ReadAllText(
            WebProjectFiles.Find("src", "app", "core", "models", "auth.models.ts"));

        var declared = Regex.Match(models, $"export const {constant} = (?<value>\\d+);");

        Assert.True(declared.Success, $"{constant} was not found in auth.models.ts.");
        Assert.Equal(expected, int.Parse(declared.Groups["value"].Value));
    }

    /// <summary>
    /// The fallback policy asks for a signed-in user, so losing the attribute would hide the notice
    /// from exactly the visitor who is deciding whether to register.
    /// </summary>
    [Fact]
    public void TheNotice_CanBeReadWithoutAnAccount()
    {
        Assert.NotNull(typeof(PrivacyController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void TheNotice_NamesWhoeverTheServerIsConfiguredWith()
    {
        var notice = Notice(new PrivacySettings
        {
            ControllerName = "  Example Person ",
            ContactEmail = "privacy@example.test"
        });

        Assert.Equal("Example Person", notice.ControllerName);
        Assert.Equal("privacy@example.test", notice.ContactEmail);
        Assert.Equal(PrivacyPolicy.Version, notice.Version);
    }

    /// <summary>Compose passes an unset variable as an empty string.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TheNotice_ReportsAnUnsetContactAsMissing(string? value)
    {
        var notice = Notice(new PrivacySettings { ControllerName = value, ContactEmail = value });

        Assert.Null(notice.ControllerName);
        Assert.Null(notice.ContactEmail);
    }

    private static StrengthPlanner.Application.DTOs.Privacy.PrivacyNoticeDto Notice(PrivacySettings settings)
    {
        return new PrivacyController(Options.Create(settings)).Get().Value!;
    }
}
