using Crm.Application.Common.Paging;

namespace Crm.Application.Customers;

/// <summary>
/// Customer profiles (reads need <c>customers.view</c>, writes <c>customers.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404 (unknown or deleted customer).
/// </summary>
public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken);

    Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: the customer disappears from lists and lookups; the row (and later its tickets) stays.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
