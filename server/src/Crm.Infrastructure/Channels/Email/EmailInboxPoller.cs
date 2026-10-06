using Crm.Application.Channels;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>
/// Recurring job (CRM-24): reads new emails from the support mailbox and hands them to the inbound pipeline. A plain
/// class — the channel worker (or a Hangfire recurring job later) calls <see cref="PollAsync"/>; tests call it directly.
/// </summary>
public sealed class EmailInboxPoller(
    EmailChannelOptions options,
    IImapMailbox mailbox,
    IInboundMessageProcessor processor,
    TimeProvider timeProvider,
    ILogger<EmailInboxPoller> logger)
{
    /// <summary>Reads unseen emails; returns how many were read (0 when IMAP is not configured).</summary>
    public async Task<int> PollAsync(CancellationToken cancellationToken)
    {
        if (!options.IsImapConfigured)
        {
            return 0;
        }

        return await mailbox.ReadUnseenAsync(options.Imap, options.Imap.BatchSize, HandleAsync, cancellationToken);
    }

    /// <summary>True when the email was taken in (or is unusable / a duplicate) and may be marked seen.</summary>
    private async Task<bool> HandleAsync(MimeMessage mail, CancellationToken cancellationToken)
    {
        var message = EmailMessageParser.Parse(mail, timeProvider.GetUtcNow().UtcDateTime);
        if (message is null)
        {
            logger.LogWarning("Skipped email {MessageId} without a sender address.", mail.MessageId);
            return true;
        }

        try
        {
            await processor.ProcessAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Stays unseen: the next poll tries again.
            logger.LogError(exception, "Could not process email {MessageId}.", message.ExternalId);
            return false;
        }
    }
}
