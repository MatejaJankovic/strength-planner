namespace StrengthPlanner.Application.DTOs.Privacy;

/// <summary>
/// The parts of the privacy notice that belong to the server it is shown on: who runs it and which
/// version of the text applies.
/// </summary>
public class PrivacyNoticeDto
{
    public string Version { get; set; } = string.Empty;

    /// <summary>Null when this server has not been configured with it.</summary>
    public string? ControllerName { get; set; }

    /// <summary>Null when this server has not been configured with it.</summary>
    public string? ContactEmail { get; set; }
}
