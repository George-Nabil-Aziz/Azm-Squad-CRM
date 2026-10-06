using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Sla;

/// <summary>EF Core storage of the SLA policies (one row per priority).</summary>
public sealed class SlaPolicyRepository(CrmDbContext db) : ISlaPolicyRepository
{
    public async Task<IReadOnlyList<SlaPolicy>> ListAsync(CancellationToken cancellationToken)
    {
        // Three rows; ordered by the enum value (High → Low) in memory because the column holds the name.
        var list = await db.SlaPolicies.AsNoTracking().ToListAsync(cancellationToken);
        return [.. list.OrderBy(p => p.Priority)];
    }

    public Task<SlaPolicy?> FindAsync(TicketPriority priority, CancellationToken cancellationToken) =>
        db.SlaPolicies.FirstOrDefaultAsync(p => p.Priority == priority, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
