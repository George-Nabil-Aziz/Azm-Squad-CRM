using System.Net;
using Crm.Application.Branding;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>
/// The body of an outgoing email with the company branding (CRM-63): a header band in the primary colour with the logo
/// (embedded, <c>cid:</c>) around the text. The plain-text part is always kept. Without any branding the body is the plain
/// text, as before.
/// </summary>
public static class BrandedEmail
{
    public static MimeEntity Build(string text, EmailBranding? branding)
    {
        var primary = SafeColor(branding?.PrimaryColor);
        var secondary = SafeColor(branding?.SecondaryColor);
        var hasLogo = branding?.Logo is { Length: > 0 } && branding.LogoContentType is { Length: > 0 };
        if (primary is null && secondary is null && !hasLogo)
        {
            return new TextPart(MimeKit.Text.TextFormat.Plain) { Text = text };
        }

        var builder = new BodyBuilder { TextBody = text };
        var logoTag = string.Empty;
        if (hasLogo)
        {
            var image = builder.LinkedResources.Add("logo", branding!.Logo!, ContentType.Parse(branding.LogoContentType!));
            image.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            logoTag = $"<img src=\"cid:{image.ContentId}\" alt=\"\" style=\"max-height:48px;vertical-align:middle\">";
        }

        var band = primary ?? "#444444";
        var accent = secondary ?? band;
        var html = WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>\n");
        builder.HtmlBody =
            "<div style=\"font-family:Arial,sans-serif;max-width:640px\">"
            + $"<div style=\"background:{band};padding:12px 16px\">{logoTag}</div>"
            + $"<div style=\"padding:16px;border-bottom:4px solid {accent}\">{html}</div>"
            + "</div>";
        return builder.ToMessageBody();
    }

    /// <summary>The colour only when it is a plain hex colour (it is written into HTML).</summary>
    private static string? SafeColor(string? color) => BrandingRules.IsValidColor(color) ? BrandingRules.NormalizeColor(color!) : null;
}
