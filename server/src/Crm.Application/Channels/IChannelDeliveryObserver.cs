using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>
/// Told whenever an outgoing message changes its delivery state (sent, failed, retried, status webhook). Called before
/// the message is saved, so the observer's own changes join the same unit of work. The ticket feature uses it to keep the
/// delivery status of a reply (<see cref="OutboundMessage.SourceId"/>) up to date.
/// </summary>
public interface IChannelDeliveryObserver
{
    Task OnDeliveryChangedAsync(OutboundMessage message, CancellationToken cancellationToken);
}
