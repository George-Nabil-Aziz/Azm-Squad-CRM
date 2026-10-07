using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>
/// EF Core storage of customers. The "SoftDelete" query filter hides deleted customers from every query here; the
/// contacts are owned by the customer, so they are always loaded with it.
/// </summary>
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
            // A search that is a phone number ("050 123 4567") also finds its stored E.164 form ("+966501234567").
            var phone = ContactValues.TryNormalizePhone(search, out var e164) ? e164 : null;
            customers = customers.Where(c =>
                EF.Functions.Like(c.Name, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Email!, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Phone!, pattern, LikePattern.EscapeCharacter)
                || c.Contacts.Any(x => EF.Functions.Like(x.Value, pattern, LikePattern.EscapeCharacter)
                                       || (phone != null && x.Value == phone)));
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

    public async Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking()
            .Where(c => c.Contacts.Any(x => types.Contains(x.Type) && x.Value == value))
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public Task MoveTicketsToBranchAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken) =>
        db.Tickets.Where(t => t.CustomerId == customerId)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.BranchId, branchId), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
