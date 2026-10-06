using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>Storage of received channel messages (EF Core in Crm.Infrastructure).</summary>
public interface IReceivedMessageRepository
{
    Task<bool> ExistsAsync(ChannelKind channel, string externalId, CancellationToken cancellationToken);

    /// <summary>When the last message from this sender arrived on the channel (the WhatsApp 24-hour window), or null.</summary>
    Task<DateTime?> LastReceivedAtAsync(ChannelKind channel, string from, CancellationToken cancellationToken);

    void Add(ReceivedMessage message);

    /// <summary>Saves; false when another request stored the same message first (unique channel + external id).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
