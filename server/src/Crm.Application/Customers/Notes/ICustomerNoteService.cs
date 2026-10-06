using Crm.Application.Common.Paging;

namespace Crm.Application.Customers.Notes;

/// <summary>
/// Notes about a customer (list needs <c>customers.view</c>, add <c>customers.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404 (unknown or deleted customer).
/// </summary>
public interface ICustomerNoteService
{
    Task<PagedResult<CustomerNoteResponse>> ListAsync(Guid customerId, ListCustomerNotesQuery query, CancellationToken cancellationToken);

    /// <summary>Adds a note by the signed-in user and a "noteAdded" timeline entry.</summary>
    Task<CustomerNoteResponse> AddAsync(Guid customerId, CustomerNoteRequest request, CancellationToken cancellationToken);
}
