using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>Body of PUT /api/tickets/{id}/status: the status to move to ("open", "pending", "resolved", "closed").</summary>
public sealed record ChangeTicketStatusRequest(string? Status);

/// <summary>
/// Moving a ticket along the status workflow. Failures: <c>NotFoundException</c> 404 (unknown ticket),
/// <c>ValidationException</c> 400 on <c>status</c> (unknown name, the current status, or a move the workflow does not allow).
/// </summary>
public interface ITicketStatusService
{
    Task<TicketResponse> ChangeAsync(Guid ticketId, ChangeTicketStatusRequest request, CancellationToken cancellationToken);
}

public sealed class TicketStatusService(
    ITicketRepository tickets,
    ITicketHistoryRecorder history,
    TimeProvider timeProvider,
    Crm.Application.Portal.ISurveyService? surveys = null) : ITicketStatusService
{
    public async Task<TicketResponse> ChangeAsync(Guid ticketId, ChangeTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        if (!TicketValues.TryParseStatus(request.Status, out var target))
        {
            throw StatusError(TicketText.StatusTargetInvalid);
        }

        var from = ticket.Status;
        if (!TicketStatusRules.CanMove(from, target))
        {
            throw StatusError(TicketText.StatusTransitionInvalid(
                TicketValues.StatusName(from),
                TicketStatusRules.AllowedTargets(from).Select(TicketValues.StatusName)));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        ticket.ChangeStatus(target, now);
        history.Record(ticket.Id, TicketHistoryField.Status, TicketValues.StatusName(from), TicketValues.StatusName(target), now);
        await tickets.SaveChangesAsync(cancellationToken);
        if (target == TicketStatus.Resolved && surveys is not null)
        {
            await surveys.OnTicketResolvedAsync(ticket, cancellationToken); // CRM-44: the customer gets the satisfaction survey
        }

        return TicketService.ToResponse(await tickets.GetViewAsync(ticketId, cancellationToken)
                                        ?? throw new InvalidOperationException("The saved ticket was not found."));
    }

    private static ValidationException StatusError(string message) =>
        new(new Dictionary<string, string[]> { ["status"] = [message] });
}
