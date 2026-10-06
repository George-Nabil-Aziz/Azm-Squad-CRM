using Crm.Application.Tickets;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of the agent dashboard (CRM-29).</summary>
public sealed class MyTicketsRepository(CrmDbContext db) : IMyTicketsRepository
{
    private readonly TicketRepository _tickets = new(db);

    public async Task<IReadOnlyList<TicketView>> ListOpenAssignedAsync(Guid userId, CancellationToken cancellationToken) =>
        [.. (await _tickets.Rows()
                .Where(r => r.Ticket.AssigneeId == userId && r.Ticket.Status != TicketStatus.Closed)
                .ToListAsync(cancellationToken))
            .Select(r => r.ToView())];

    public Task<int> CountBreachedTodayAsync(Guid userId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken cancellationToken) =>
        (from slaEvent in db.TicketSlaEvents
         join ticket in db.Tickets on slaEvent.TicketId equals ticket.Id
         where ticket.AssigneeId == userId
               && ticket.Status != TicketStatus.Closed
               && (slaEvent.Type == SlaEventType.ResponseBreached || slaEvent.Type == SlaEventType.ResolutionBreached)
               && slaEvent.OccurredAt >= dayStartUtc && slaEvent.OccurredAt < dayEndUtc
         select slaEvent.TicketId).Distinct().CountAsync(cancellationToken);
}
