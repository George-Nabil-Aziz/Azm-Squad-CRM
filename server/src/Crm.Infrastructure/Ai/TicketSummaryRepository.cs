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

/// <summary>EF Core storage of the AI classifications; they are saved by the ticket's own save.</summary>
public sealed class AiClassificationRepository(CrmDbContext db) : ITicketAiClassificationRepository
{
    public Task<TicketAiClassification?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.TicketAiClassifications.FirstOrDefaultAsync(c => c.TicketId == ticketId, cancellationToken);

    public void Add(TicketAiClassification classification) => db.TicketAiClassifications.Add(classification);
}
