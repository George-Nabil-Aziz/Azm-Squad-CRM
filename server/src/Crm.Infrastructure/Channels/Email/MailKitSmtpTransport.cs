using Crm.Application.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Real SMTP transport: connects, authenticates when a user name is set, sends, disconnects (one connection per message).</summary>
public sealed class MailKitSmtpTransport : ISmtpTransport
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task SendAsync(MimeMessage message, EmailChannelOptions.SmtpSettings settings, CancellationToken cancellationToken)
    {
        var host = settings.Host ?? throw new InvalidOperationException("Channels:Email:Smtp:Host is not configured.");
        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(
            host, settings.Port, MailKitSecurity.Parse(settings.Security, SecureSocketOptions.StartTls), cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
