namespace Crm.Application.Portal;

/// <summary>
/// Settings of the customer portal, bound from the <c>Portal</c> configuration section. None of them is a secret.
/// </summary>
public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    /// <summary>
    /// Public address of the portal web app (e.g. "https://support.example.com"), used for links in emails and in
    /// replies. Empty = relative links ("/portal/...").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>A customer may reopen a resolved ticket within this many days of its resolution (CRM-42).</summary>
    public int ReopenWindowDays { get; set; } = 7;

    /// <summary>A satisfaction survey link is valid for this many days after it was sent (CRM-44).</summary>
    public int SurveyValidDays { get; set; } = 7;

    /// <summary>Log the sign-in code at Information level so a developer can sign in without a mail channel. Only ever true in Development (or Testing with the flag); the infrastructure forces it off elsewhere.</summary>
    public bool LogLoginCodes { get; set; }

    /// <summary>"{BaseUrl}{path}" without a doubled slash; just the path when no base address is set.</summary>
    public string Link(string path) => BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
}
