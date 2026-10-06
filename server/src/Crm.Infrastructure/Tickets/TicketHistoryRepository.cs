using Crm.Application.Tickets;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of ticket history entries; reading merges in the ticket SLA escalations.</summary>
public sealed class TicketHistoryRepository(CrmDbContext db) : ITicketHistoryRepository
{
    public void Add(TicketHistoryEntry entry) => db.TicketHistory.Add(entry);

    public async Task<IReadOnlyList<TicketHistoryItemResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var entries = await db.TicketHistory.AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .Select(h => new
            {
                Entry = h,
                UserName = db.Users.Where(u => u.Id == h.ChangedById).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var escalations = await db.TicketSlaEvents.AsNoTracking()
            .Where(e => e.TicketId == ticketId && e.Type == SlaEventType.Escalated)
            .ToListAsync(cancellationToken);

        var items = entries
            .Select(x => (Order: (long)x.Entry.Id, Item: new TicketHistoryItemResponse(
                $"h{x.Entry.Id}", FieldName(x.Entry.Field), x.Entry.OldValue, x.Entry.NewValue,
                x.Entry.ChangedById, x.UserName, x.Entry.ChangedAt)))
            .Concat(escalations.Select(e => (Order: long.MaxValue, Item: new TicketHistoryItemResponse(
                $"s{e.Id}", "escalation", null, e.Level.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null, e.OccurredAt))));

        // SQLite / SQL Server cannot order DateTime ties by insertion across two tables: sort in memory (one ticket).
        return [.. items.OrderBy(i => i.Item.ChangedAt).ThenBy(i => i.Order).Select(i => i.Item)];
    }

    private static string FieldName(TicketHistoryField field) => field.ToString().ToLowerInvariant();
}
