using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Sla;

/// <summary>EF Core storage for the SLA monitor job.</summary>
public sealed class TicketSlaRepository(CrmDbContext db) : ITicketSlaRepository
{
    public async Task<IReadOnlyList<Ticket>> ListBreachCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        await db.Tickets
            .Where(t =>
                (!t.ResponseBreached && t.ResponseDueAt != null
                    && ((t.FirstResponseAt == null && t.ResponseDueAt <= utcNow) || t.FirstResponseAt > t.ResponseDueAt))
                || (!t.ResolutionBreached && t.ResolutionDueAt != null
                    && ((t.ResolvedAt == null && t.ResolutionDueAt <= utcNow) || t.ResolvedAt > t.ResolutionDueAt)))
            .OrderBy(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void AddEvent(TicketSlaEvent slaEvent) => db.TicketSlaEvents.Add(slaEvent);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another run stored the same events first (unique index): nothing of this run was saved.
            return false;
        }
    }
}
