using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>Ticket storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ITicketRepository
{
    /// <summary>True when the customer exists and is not deleted (only such customers get new tickets).</summary>
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>The number the next ticket gets: the highest saved number + 1 (1 for the first ticket).</summary>
    Task<int> NextNumberAsync(CancellationToken cancellationToken);

    void Add(Ticket ticket);

    /// <summary>
    /// Saves every pending change of the unit of work (the ticket and its timeline entry together). Throws
    /// <c>ConflictException</c> when another ticket was saved with the same number in the meantime (unique index).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>The ticket with its customer name (also of a deleted customer), category and assignee names; or null.</summary>
    Task<TicketView?> GetViewAsync(Guid id, CancellationToken cancellationToken);
}
