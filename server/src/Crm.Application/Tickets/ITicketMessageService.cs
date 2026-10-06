namespace Crm.Application.Tickets;

/// <summary>
/// A ticket conversation (reading needs <c>tickets.view</c>, writing <c>tickets.manage</c>; enforced by the API).
/// Failures: <c>NotFoundException</c> 404 (unknown ticket), <c>ValidationException</c> 400 (blank / too long body, closed ticket).
/// </summary>
public interface ITicketMessageService
{
    /// <summary>The whole thread for staff (internal notes included), oldest first.</summary>
    Task<IReadOnlyList<TicketMessageResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>The thread as the customer may see it: internal notes left out. For the portal and channel stories.</summary>
    Task<IReadOnlyList<TicketMessageResponse>> ListCustomerVisibleAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Adds an agent reply (or an internal note) as the signed-in user; the first public reply sets FirstResponseAt.</summary>
    Task<TicketMessageResponse> AddAsync(Guid ticketId, AddTicketMessageRequest request, CancellationToken cancellationToken);
}
