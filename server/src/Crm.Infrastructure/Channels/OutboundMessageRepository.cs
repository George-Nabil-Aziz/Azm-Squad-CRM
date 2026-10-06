using Crm.Application.Channels;
using Crm.Domain.Channels;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Channels;

/// <summary>EF Core storage of outgoing channel messages.</summary>
public sealed class OutboundMessageRepository(CrmDbContext db) : IOutboundMessageRepository
{
    public void Add(OutboundMessage message) => db.OutboundMessages.Add(message);

    public Task<OutboundMessage?> FindByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken) =>
        db.OutboundMessages.FirstOrDefaultAsync(m => m.ProviderMessageId == providerMessageId, cancellationToken);

    public async Task<IReadOnlyList<OutboundMessage>> ListDueForRetryAsync(DateTime utcNow, int max, CancellationToken cancellationToken) =>
        await db.OutboundMessages
            .Where(m => m.Status == DeliveryStatus.Failed && m.NextAttemptAt != null && m.NextAttemptAt <= utcNow)
            .OrderBy(m => m.NextAttemptAt)
            .Take(max)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
