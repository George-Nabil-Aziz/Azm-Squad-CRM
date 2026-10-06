namespace Crm.Application.Channels;

/// <summary>
/// Takes in a customer message from any channel: ignores duplicates, matches the sender to a customer (or creates
/// one), reads the ticket number from the subject and stores the message. Ticket creation is added by Phase 2
/// (after CRM-15).
/// </summary>
public interface IInboundMessageProcessor
{
    Task<InboundResult> ProcessAsync(InboundChannelMessage message, CancellationToken cancellationToken);
}
