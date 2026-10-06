using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>The audit trail of a ticket (read-only; reading needs <c>tickets.view</c>). Unknown ticket: <c>NotFoundException</c> 404.</summary>
public interface ITicketHistoryService
{
    /// <summary>Every recorded change (and SLA escalation), oldest first.</summary>
    Task<IReadOnlyList<TicketHistoryItemResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class TicketHistoryService(ITicketRepository tickets, ITicketHistoryRepository history) : ITicketHistoryService
{
    public async Task<IReadOnlyList<TicketHistoryItemResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        _ = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        return await history.ListAsync(ticketId, cancellationToken);
    }
}

/// <summary>Changing a ticket category. Failures: 404 unknown ticket, 400 on <c>categoryId</c> for an unknown or inactive category.</summary>
public interface ITicketCategoryChangeService
{
    Task<TicketResponse> ChangeAsync(Guid ticketId, ChangeTicketCategoryRequest request, CancellationToken cancellationToken);
}

public sealed class TicketCategoryChangeService(
    ITicketRepository tickets,
    ITicketCategoryRepository categories,
    ITicketHistoryRecorder history,
    TimeProvider timeProvider) : ITicketCategoryChangeService
{
    public async Task<TicketResponse> ChangeAsync(Guid ticketId, ChangeTicketCategoryRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        var current = await tickets.GetViewAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        if (ticket.CategoryId == request.CategoryId)
        {
            return TicketService.ToResponse(current);
        }

        TicketCategory? category = null;
        if (request.CategoryId is { } categoryId)
        {
            category = await categories.FindAsync(categoryId, cancellationToken);
            if (category is not { IsActive: true })
            {
                throw new ValidationException(new Dictionary<string, string[]> { ["categoryId"] = [TicketText.CategoryUnavailable] });
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        ticket.ChangeCategory(category?.Id, now);
        history.Record(ticket.Id, TicketHistoryField.Category, current.CategoryName, category?.Name, now);
        await tickets.SaveChangesAsync(cancellationToken);

        return TicketService.ToResponse(await tickets.GetViewAsync(ticketId, cancellationToken)
                                        ?? throw new InvalidOperationException("The saved ticket was not found."));
    }
}
