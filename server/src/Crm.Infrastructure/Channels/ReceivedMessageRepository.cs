using Crm.Application.Channels;
using Crm.Domain.Channels;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Channels;

/// <summary>EF Core storage of received channel messages.</summary>
public sealed class ReceivedMessageRepository(CrmDbContext db) : IReceivedMessageRepository
{
    public Task<bool> ExistsAsync(ChannelKind channel, string externalId, CancellationToken cancellationToken) =>
        db.ReceivedMessages.AnyAsync(m => m.Channel == channel && m.ExternalId == externalId, cancellationToken);

    public Task<DateTime?> LastReceivedAtAsync(ChannelKind channel, string from, CancellationToken cancellationToken) =>
        db.ReceivedMessages
            .Where(m => m.Channel == channel && m.From == from)
            .OrderByDescending(m => m.ReceivedAt)
            .Select(m => (DateTime?)m.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(ReceivedMessage message) => db.ReceivedMessages.Add(message);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (db.ChangeTracker.Entries<ReceivedMessage>().Any(e => e.State == EntityState.Added))
        {
            // Unique (Channel, ExternalId): another run stored the same message first.
            foreach (var entry in db.ChangeTracker.Entries<ReceivedMessage>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
