using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>Ticket category storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ITicketCategoryRepository
{
    /// <summary>Every category (or only the active ones), ordered by name. Not tracked.</summary>
    Task<IReadOnlyList<TicketCategory>> ListAsync(bool activeOnly, CancellationToken cancellationToken);

    /// <summary>The category (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<TicketCategory?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when another category (not <paramref name="exceptId"/>) has this normalized name.</summary>
    Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    void Add(TicketCategory category);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
