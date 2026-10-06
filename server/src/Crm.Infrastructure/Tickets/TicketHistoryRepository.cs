using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of ticket history entries.</summary>
public sealed class TicketHistoryRepository(CrmDbContext db) : ITicketHistoryRepository
{
    public void Add(TicketHistoryEntry entry) => db.TicketHistory.Add(entry);
}
