using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of ticket categories.</summary>
public sealed class TicketCategoryRepository(CrmDbContext db) : ITicketCategoryRepository
{
    public async Task<IReadOnlyList<TicketCategory>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.TicketCategories.AsNoTracking()
            .Where(c => !activeOnly || c.IsActive)
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<TicketCategory?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.TicketCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.TicketCategories.AnyAsync(c => c.NormalizedName == normalizedName && c.Id != exceptId, cancellationToken);

    public void Add(TicketCategory category) => db.TicketCategories.Add(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
