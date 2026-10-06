using Crm.Application.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of the ticket customer panel (CRM-30). A deleted customer is read on purpose (ticket views do too).</summary>
public sealed class CustomerContextRepository(CrmDbContext db) : ICustomerContextRepository
{
    public async Task<CustomerContextData?> FindAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var customerId = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => (Guid?)t.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is not { } id)
        {
            return null;
        }

        var customer = await db.Customers.AsNoTracking()
            .IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])
            .FirstAsync(c => c.Id == id, cancellationToken);
        var tickets = db.Tickets.AsNoTracking().Where(t => t.CustomerId == id);
        var total = await tickets.CountAsync(cancellationToken);
        var recent = await tickets
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number)
            .Take(CustomerContextData.RecentCount)
            .ToListAsync(cancellationToken);
        return new CustomerContextData(customer, customer.IsDeleted, total, recent);
    }
}
