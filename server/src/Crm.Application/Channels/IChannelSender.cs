namespace Crm.Application.Channels;

/// <summary>
/// The one way the CRM sends to customers: picks the <see cref="IChannelProvider"/> of the channel, logs the message as
/// an <c>OutboundMessage</c> (sent / failed), retries failures and applies delivery statuses from webhooks.
/// </summary>
public interface IChannelSender
{
    /// <summary>
    /// Sends the reply now and returns the logged message. A provider failure is not thrown: the message is stored as
    /// "failed" and retried by <see cref="RetryDueAsync"/>.
    /// </summary>
    Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken);

    /// <summary>Sends failed messages whose next attempt is due again (recurring job). Returns how many were tried.</summary>
    Task<int> RetryDueAsync(CancellationToken cancellationToken);

    ChannelStatusResponse GetStatus();
}
