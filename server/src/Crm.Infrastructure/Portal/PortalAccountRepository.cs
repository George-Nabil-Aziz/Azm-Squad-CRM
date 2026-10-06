using Crm.Application.Portal;
using Crm.Domain.Portal;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Portal;

/// <summary>EF Core storage of portal accounts and sign-in codes.</summary>
public sealed class PortalAccountRepository(CrmDbContext db) : IPortalAccountRepository
{
    public Task<PortalLoginCode?> FindLatestCodeAsync(string email, CancellationToken cancellationToken) =>
        db.PortalLoginCodes.Where(c => c.Email == email)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PortalLoginCode>> ListUnusedCodesAsync(string email, CancellationToken cancellationToken) =>
        await db.PortalLoginCodes.Where(c => c.Email == email && c.ConsumedAt == null).ToListAsync(cancellationToken);

    public void AddCode(PortalLoginCode code) => db.PortalLoginCodes.Add(code);

    public Task<PortalAccount?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.PortalAccounts.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

    public void AddAccount(PortalAccount account) => db.PortalAccounts.Add(account);

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
