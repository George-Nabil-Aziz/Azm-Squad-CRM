using Crm.Application.Common.Paging;

namespace Crm.Application.Customers;

/// <summary>
/// Customer profiles (reads need <c>customers.view</c>, writes <c>customers.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404 (unknown or deleted customer / unknown contact),
/// <c>ConflictException</c> 409 (duplicate contact).
/// </summary>
public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken);

    Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: the customer disappears from lists and lookups; the row (and later its tickets) stays.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds a phone, email or WhatsApp contact (phones stored as E.164). 409 when the customer already has it.</summary>
    Task<CustomerContactResponse> AddContactAsync(Guid customerId, CustomerContactRequest request, CancellationToken cancellationToken);

    /// <summary>Makes the contact the primary one of its type; the old primary is unset.</summary>
    Task MakeContactPrimaryAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>Removes the contact; when it was primary, the oldest other contact of its type becomes primary.</summary>
    Task RemoveContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>
    /// Customers with exactly this phone (phone or WhatsApp contact) or email. Used by GET /api/customers/lookup and by
    /// the email / WhatsApp channels to match an incoming message to its customer. Empty list when nobody matches.
    /// </summary>
    Task<IReadOnlyList<CustomerResponse>> LookupAsync(CustomerLookupQuery query, CancellationToken cancellationToken);
}
