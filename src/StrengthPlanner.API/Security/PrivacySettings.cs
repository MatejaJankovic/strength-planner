namespace StrengthPlanner.API.Security;

/// <summary>
/// Who answers for the data on this server. Read from configuration (<c>Privacy__ControllerName</c>,
/// <c>Privacy__ContactEmail</c>) so that a person's name and address stay out of the repository.
/// </summary>
public class PrivacySettings
{
    public const string SectionName = "Privacy";

    public string? ControllerName { get; set; }

    public string? ContactEmail { get; set; }
}
