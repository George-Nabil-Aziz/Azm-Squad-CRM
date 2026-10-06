using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>EF Core storage of customers. The "SoftDelete" query filter hides deleted customers from every query here.</summary>
public sealed class CustomerRepository(CrmDbContext db) : ICustomerRepository
{
    public async Task<PagedResult<Customer>> ListAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var customers = db.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite (ASCII); wildcards are escaped.
            var pattern = LikePattern.Contains(search);
            customers = customers.Where(c =>
                EF.Functions.Like(c.Name, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Email!, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Phone!, pattern, LikePattern.EscapeCharacter));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer>(items, page, pageSize, totalCount);
    }

    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
