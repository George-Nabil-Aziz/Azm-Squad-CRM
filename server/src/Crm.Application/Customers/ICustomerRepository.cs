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
    /// One page of customers whose name, phone or email contains <paramref name="search"/> (case-insensitive,
    /// wildcards taken literally; null = every customer), ordered by name. <c>TotalCount</c> counts every match.
    /// </summary>
    Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The customer (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(Customer customer);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
