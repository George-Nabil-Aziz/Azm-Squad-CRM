namespace Crm.Application.Channels;

/// <summary>
/// Settings section <c>Channels:WhatsApp</c> (Meta WhatsApp Cloud API). <c>AccessToken</c>, <c>AppSecret</c> and
/// <c>VerifyToken</c> only in user-secrets / environment variables (e.g. <c>Channels__WhatsApp__AccessToken</c>).
/// Missing values never stop the app: sending reports "not configured", the webhook answers 403 / 401.
/// </summary>
public sealed class WhatsAppChannelOptions
{
    public const string SectionName = "Channels:WhatsApp";

    /// <summary>Cloud API phone number id of the business number (sender).</summary>
    public string? PhoneNumberId { get; set; }

    /// <summary>System-user access token of the Meta app.</summary>
    public string? AccessToken { get; set; }

    /// <summary>App secret: signs webhook calls (X-Hub-Signature-256).</summary>
    public string? AppSecret { get; set; }

    /// <summary>Token chosen when registering the webhook URL (hub.verify_token).</summary>
    public string? VerifyToken { get; set; }

    /// <summary>Graph API base address, version included.</summary>
    public string ApiBaseUrl { get; set; } = "https://graph.facebook.com/v21.0/";

    /// <summary>Language code of approved templates (e.g. "en", "ar").</summary>
    public string TemplateLanguage { get; set; } = "en";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(AccessToken);
}
