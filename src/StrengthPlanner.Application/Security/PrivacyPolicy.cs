namespace StrengthPlanner.Application.Security;

public static class PrivacyPolicy
{
    /// <summary>
    /// Version of the privacy notice, as the date it took effect. It is stored with each account's
    /// consent, so it must change whenever the notice (<c>features/privacy/privacy-page.html</c>)
    /// changes what is collected, why, for how long or who receives it.
    /// </summary>
    public const string Version = "2026-11-28";
}
