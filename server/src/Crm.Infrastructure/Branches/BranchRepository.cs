using Crm.Application.Branches;
using Crm.Domain.Branches;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Branches;

/// <summary>EF Core storage of branches.</summary>
public sealed class BranchRepository(CrmDbContext db) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.Branches.AsNoTracking()
            .Where(b => !activeOnly || b.IsActive)
            .OrderBy(b => b.Name).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    public Task<Branch?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Branches.AnyAsync(b => b.NormalizedName == normalizedName && b.Id != exceptId, cancellationToken);

    public void Add(Branch branch) => db.Branches.Add(branch);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
