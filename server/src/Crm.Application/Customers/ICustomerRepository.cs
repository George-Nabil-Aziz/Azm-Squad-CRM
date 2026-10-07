using Crm.Application.Common.Paging;
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

/// <summary>
/// Customer storage (implemented in Crm.Infrastructure with EF Core). Deleted customers are never returned: the
/// EF "SoftDelete" query filter hides them.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// One page of customers whose name or any contact value contains <paramref name="search"/> (case-insensitive,
    /// wildcards taken literally; null = every customer), ordered by name. <c>TotalCount</c> counts every match.
    /// </summary>
    Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The customer (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Customers that have a contact of one of <paramref name="types"/> whose stored value equals
    /// <paramref name="value"/> (already normalized: E.164 / lower-case email), ordered by name. Not tracked.
    /// </summary>
    Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken);

    void Add(Customer customer);

    /// <summary>Gives every ticket of the customer the branch (CRM-62: tickets follow their customer).</summary>
    Task MoveTicketsToBranchAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
