using Crm.Application.Common.Paging;
using Crm.Domain.Customers;

namespace Crm.Application.Customers.Timeline;

/// <summary>Timeline storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ICustomerTimelineRepository
{
    /// <summary>Adds the entry to the current unit of work; it is saved by the caller's next SaveChangesAsync.</summary>
    void Add(CustomerInteraction interaction);

    /// <summary>
    /// One page of the customer's entries (only <paramref name="type"/> when given), newest first; entries of the same
    /// time in reverse insertion order. <c>TotalCount</c> counts every matching entry.
    /// </summary>
    Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, InteractionType? type, int page, int pageSize, CancellationToken cancellationToken);
}
