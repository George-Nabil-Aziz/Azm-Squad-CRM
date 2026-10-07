using Crm.Application.Branding;
using Crm.Application.Channels;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Builds the MIME email of an outgoing message (plain text, UTF-8, our own Message-Id).</summary>
public static class EmailMessageBuilder
{
    public static MimeMessage Build(OutboundChannelMessage message, EmailChannelOptions options, EmailBranding? branding = null)
    {
        var fromAddress = options.FromAddress ?? throw new InvalidOperationException("Channels:Email:FromAddress is not configured.");
        var domain = fromAddress[(fromAddress.LastIndexOf('@') + 1)..];

        var mail = new MimeMessage
        {
            Subject = message.Subject ?? string.Empty,
            // Stable across retries (the OutboundMessage id), so a customer never gets two different ids for one reply.
            MessageId = $"{message.Id:N}@{domain}",
            Body = BrandedEmail.Build(message.Body, branding),
        };
        mail.From.Add(new MailboxAddress(options.FromName ?? string.Empty, fromAddress));
        mail.To.Add(MailboxAddress.Parse(message.Recipient));
        return mail;
    }
}
