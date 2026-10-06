using Crm.Application.Common.Paging;
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

    /// <summary>The ticket (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The active staff user (with a role) a ticket can be assigned to, or null for an unknown, inactive or role-less user.</summary>
    Task<TicketAssigneeResponse?> FindAssigneeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The ticket with its customer name (also of a deleted customer), category and assignee names; or null.</summary>
    Task<TicketView?> GetViewAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the tickets matching <b>every</b> filter, newest first (CreatedAt, then Number, descending);
    /// tickets of deleted customers included. <c>TotalCount</c> counts every match.
    /// </summary>
    Task<PagedResult<TicketView>> ListAsync(TicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Active staff users (with at least one role), ordered by name.</summary>
    Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken);
}
