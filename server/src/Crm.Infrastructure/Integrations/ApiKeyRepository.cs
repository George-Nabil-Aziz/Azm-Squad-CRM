using Crm.Application.Integrations;
using Crm.Domain.Integrations;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Integrations;

/// <summary>EF Core storage of API keys (CRM-58).</summary>
public sealed class ApiKeyRepository(CrmDbContext db) : IApiKeyRepository
{
    public void Add(ApiKey key) => db.ApiKeys.Add(key);

    public Task<ApiKey?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken) =>
        db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken) =>
        await db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ThenBy(k => k.Id).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
