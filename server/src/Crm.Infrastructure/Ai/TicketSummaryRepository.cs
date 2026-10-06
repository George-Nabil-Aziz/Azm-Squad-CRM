using Crm.Application.Ai;
using Crm.Domain.Ai;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Ai;

/// <summary>EF Core storage of the AI summaries of tickets.</summary>
public sealed class TicketSummaryRepository(CrmDbContext db) : ITicketSummaryRepository
{
    public Task<TicketAiSummary?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.TicketAiSummaries.FirstOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);

    public void Add(TicketAiSummary summary) => db.TicketAiSummaries.Add(summary);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
