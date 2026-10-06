using Crm.Application.Common.Paging;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>EF Core storage of timeline entries; the actor's name comes from the Identity users table.</summary>
public sealed class CustomerTimelineRepository(CrmDbContext db) : ICustomerTimelineRepository
{
    public void Add(CustomerInteraction interaction) => db.CustomerInteractions.Add(interaction);

    public async Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, InteractionType? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        var entries = db.CustomerInteractions.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (type is not null)
        {
            entries = entries.Where(x => x.Type == type);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rows = await entries
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                Entry = x,
                ActorName = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerInteractionResponse>(
            [.. rows.Select(row => new CustomerInteractionResponse(
                row.Entry.Id, InteractionTypes.Name(row.Entry.Type), row.Entry.Event, row.Entry.Details, row.Entry.SourceId,
                row.Entry.ActorId, row.ActorName, row.Entry.OccurredAt))],
            page,
            pageSize,
            totalCount);
    }
}
