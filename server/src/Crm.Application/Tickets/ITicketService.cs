using Crm.Application.Common.Paging;

namespace Crm.Application.Tickets;

/// <summary>
/// Tickets (reads need <c>tickets.view</c>, creating <c>tickets.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400 (missing / invalid fields, unknown customer, inactive category),
/// <c>NotFoundException</c> 404 (unknown ticket).
/// </summary>
public interface ITicketService
{
    /// <summary>Creates a ticket in status New with the next ticket number, created by the signed-in user.</summary>
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);

    Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>One page of tickets matching every given filter, newest first.</summary>
    Task<PagedResult<TicketResponse>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken);

    /// <summary>Staff users tickets can be assigned to (active, with a role), ordered by name.</summary>
    Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken);
}
