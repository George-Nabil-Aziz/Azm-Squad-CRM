using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>Storage of outgoing messages (EF Core in Crm.Infrastructure).</summary>
public interface IOutboundMessageRepository
{
    void Add(OutboundMessage message);

    /// <summary>The message the provider knows by this id (tracked), or null.</summary>
    Task<OutboundMessage?> FindByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken);

    /// <summary>Failed messages whose next attempt is due (tracked), oldest due first, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<OutboundMessage>> ListDueForRetryAsync(DateTime utcNow, int max, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
