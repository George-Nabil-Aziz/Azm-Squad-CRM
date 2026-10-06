using Crm.Application.Channels;
using Crm.Domain.Channels;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>The email channel: sends replies over SMTP (MailKit). Settings: <c>Channels:Email</c>.</summary>
public sealed class SmtpEmailProvider(EmailChannelOptions options, ISmtpTransport transport) : IChannelProvider
{
    public ChannelKind Channel => ChannelKind.Email;

    public bool IsConfigured => options.IsSmtpConfigured;

    public async Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ChannelSendResult.Fail(ChannelText.EmailNotConfigured);
        }

        try
        {
            var mail = EmailMessageBuilder.Build(message, options);
            await transport.SendAsync(mail, options.Smtp, cancellationToken);
            return ChannelSendResult.Ok(mail.MessageId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Server down, refused login, rejected recipient, invalid address …: logged on the message and retried.
            return ChannelSendResult.Fail(exception.Message);
        }
    }
}
