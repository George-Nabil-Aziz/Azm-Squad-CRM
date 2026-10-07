namespace Crm.Application.WebForms;

/// <summary>
/// Settings section <c>WebForms</c> (CRM-55). <c>CaptchaSecret</c> only in user-secrets / environment variables
/// (<c>WebForms__CaptchaSecret</c>). Without a secret the captcha check is skipped (rate limit and honeypot still apply).
/// </summary>
public sealed class WebFormOptions
{
    public const string SectionName = "WebForms";

    /// <summary>Submissions allowed per IP address in one window.</summary>
    public int RateLimitRequests { get; set; } = 5;

    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>Server-side key of the captcha provider (Cloudflare Turnstile / reCAPTCHA / hCaptcha compatible).</summary>
    public string? CaptchaSecret { get; set; }

    /// <summary>Public key the embedded form uses to render the captcha widget.</summary>
    public string? CaptchaSiteKey { get; set; }

    public string CaptchaVerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public bool CaptchaConfigured => !string.IsNullOrWhiteSpace(CaptchaSecret);
}
