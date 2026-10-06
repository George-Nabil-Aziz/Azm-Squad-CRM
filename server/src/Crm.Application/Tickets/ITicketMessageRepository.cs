using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>Ticket message storage (EF Core in Crm.Infrastructure). Saved by <see cref="ITicketRepository.SaveChangesAsync"/>.</summary>
public interface ITicketMessageRepository
{
    void Add(TicketMessage message);

    /// <summary>The tracked message (change it, then save through <see cref="ITicketRepository.SaveChangesAsync"/>), or null.</summary>
    Task<TicketMessage?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>One message with the author name, or null.</summary>
    Task<TicketMessageResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A ticket thread, oldest first; internal notes only when <paramref name="includeInternal"/>.</summary>
    Task<IReadOnlyList<TicketMessageResponse>> ListAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken);
}
