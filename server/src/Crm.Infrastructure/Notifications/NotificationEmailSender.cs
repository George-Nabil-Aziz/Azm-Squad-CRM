using Crm.Application.Channels;
using Crm.Application.Notifications;
using Crm.Infrastructure.Channels.Email;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Crm.Infrastructure.Notifications;

/// <summary>
/// Sends notification emails through the SMTP settings of the email channel (<c>Channels:Email</c>). When SMTP is not
/// configured nothing is sent (never an error). The transport is a fake in tests.
/// </summary>
public sealed class NotificationEmailSender(
    EmailChannelOptions options,
    ISmtpTransport transport,
    ILogger<NotificationEmailSender> logger) : INotificationEmailSender
{
    public async Task SendAsync(NotificationEmail email, CancellationToken cancellationToken)
    {
        if (!options.IsSmtpConfigured)
        {
            logger.LogDebug("SMTP is not configured: notification email to {Address} skipped.", email.ToAddress);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName ?? options.FromAddress, options.FromAddress!));
        message.To.Add(new MailboxAddress(email.ToName, email.ToAddress));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };
        await transport.SendAsync(message, options.Smtp, cancellationToken);
    }
}
