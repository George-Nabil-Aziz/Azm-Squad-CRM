namespace Crm.Application.Channels;

/// <summary>
/// The one way the CRM sends to customers: picks the <see cref="IChannelProvider"/> of the channel, logs the message as
/// an <c>OutboundMessage</c> (sent / failed), retries failures and applies delivery statuses from webhooks.
/// </summary>
public interface IChannelSender
{
    /// <summary>
    /// Sends the reply now and returns the logged message. A provider failure is not thrown: the message is stored as
    /// "failed" and retried by <see cref="RetryDueAsync"/>. WhatsApp free text outside the 24-hour window throws
    /// <c>ValidationException</c> on "body" (nothing is sent or stored) — a template is needed.
    /// </summary>
    Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a status reported by the provider (WhatsApp webhook) to the message with that provider id. False when no
    /// such message exists (e.g. sent outside the CRM).
    /// </summary>
    Task<bool> ApplyDeliveryStatusAsync(
        string providerMessageId, Crm.Domain.Channels.DeliveryStatus status, string? error, CancellationToken cancellationToken);

    /// <summary>Sends failed messages whose next attempt is due again (recurring job). Returns how many were tried.</summary>
    Task<int> RetryDueAsync(CancellationToken cancellationToken);

    ChannelStatusResponse GetStatus();
}
