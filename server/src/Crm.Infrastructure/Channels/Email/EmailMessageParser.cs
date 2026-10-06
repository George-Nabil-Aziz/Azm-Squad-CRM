using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Crm.Application.Channels;
using Crm.Domain.Channels;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Turns a received email into an <see cref="InboundChannelMessage"/> (sender lower case, plain text body).</summary>
public static partial class EmailMessageParser
{
    /// <summary>The message, or null when it has no sender address (nothing to match or answer).</summary>
    public static InboundChannelMessage? Parse(MimeMessage mail, DateTime receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(mail);
        var sender = mail.From.Mailboxes.FirstOrDefault() ?? mail.Sender;
        if (sender is null || string.IsNullOrWhiteSpace(sender.Address))
        {
            return null;
        }

        var from = sender.Address.Trim().ToLowerInvariant();
        var body = (mail.TextBody ?? HtmlToText(mail.HtmlBody) ?? string.Empty).Trim();
        var externalId = string.IsNullOrWhiteSpace(mail.MessageId) ? Hash(from, mail, body) : mail.MessageId.Trim();

        return new InboundChannelMessage(
            ChannelKind.Email, externalId, from, string.IsNullOrWhiteSpace(sender.Name) ? null : sender.Name.Trim(),
            mail.Subject, body, receivedAtUtc);
    }

    /// <summary>Stable id for an email without Message-Id, so polling it again is still recognised as a duplicate.</summary>
    private static string Hash(string from, MimeMessage mail, string body)
    {
        var date = mail.Date == DateTimeOffset.MinValue ? string.Empty : mail.Date.ToString("O", CultureInfo.InvariantCulture);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{from}\n{date}\n{mail.Subject}\n{body}"));
        return "sha256:" + Convert.ToHexStringLower(bytes);
    }

    private static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = LineBreakTags().Replace(html, "\n");
        text = Tags().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*(p|div|li|tr|h[1-6])\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}
