using Crm.Application.Common.Paging;

namespace Crm.Application.Customers.Timeline;

/// <summary>
/// A customer's interaction history (read needs <c>customers.view</c> — enforced by the API). Failures:
/// <c>ValidationException</c> 400 (unknown type, paging), <c>NotFoundException</c> 404 (unknown or deleted customer).
/// </summary>
public interface ICustomerTimelineService
{
    Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, CustomerTimelineQuery query, CancellationToken cancellationToken);
}
