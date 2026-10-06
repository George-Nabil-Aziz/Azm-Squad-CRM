using Crm.Application.Channels;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Hands a built email to an SMTP server. Wraps MailKit's <c>SmtpClient</c> so tests never open a connection.</summary>
public interface ISmtpTransport
{
    /// <summary>Sends the message; throws (MailKit / socket exceptions) when the server cannot be reached or rejects it.</summary>
    Task SendAsync(MimeMessage message, EmailChannelOptions.SmtpSettings settings, CancellationToken cancellationToken);
}
