using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Notifications;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>Body of POST /api/tickets/{id}/assign: the staff user to give the ticket to; null unassigns it.</summary>
public sealed record AssignTicketRequest(Guid? AssigneeId);

/// <summary>
/// Assigning tickets. Failures: <c>NotFoundException</c> 404 (unknown ticket), <c>ForbiddenException</c> 403 (assigning to
/// someone else without <c>tickets.assign</c>), <c>ValidationException</c> 400 (unknown / inactive user).
/// </summary>
public interface ITicketAssignmentService
{
    Task<TicketResponse> AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken cancellationToken);
}

public sealed class TicketAssignmentService(
    ITicketRepository tickets,
    ITicketHistoryRecorder history,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    INotificationDispatcher? notifications = null) : ITicketAssignmentService
{
    public async Task<TicketResponse> AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        var current = await tickets.GetViewAsync(ticketId, cancellationToken)
                      ?? throw new NotFoundException(TicketText.NotFound);
        EnsureAllowed(ticket, request.AssigneeId);
        if (ticket.AssigneeId == request.AssigneeId)
        {
            return TicketService.ToResponse(current); // nothing to change, nothing to record
        }

        TicketAssigneeResponse? assignee = null;
        if (request.AssigneeId is { } assigneeId)
        {
            assignee = await tickets.FindAssigneeAsync(assigneeId, cancellationToken)
                       ?? throw new ValidationException(new Dictionary<string, string[]> { ["assigneeId"] = [TicketText.AssigneeUnavailable] });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        ticket.AssignTo(assignee?.Id, now);
        history.Record(ticket.Id, TicketHistoryField.Assignee, current.AssigneeName, assignee?.FullName, now);
        await tickets.SaveChangesAsync(cancellationToken);
        if (notifications is not null && assignee is not null && assignee.Id != currentUser.UserId)
        {
            // CRM-28 AC 1: the assignee hears about it in real time (nobody is told about their own assignment).
            await notifications.NotifyAsync(AssignmentNotifications.For(ticket.Id, assignee.Id, now), cancellationToken);
        }

        return TicketService.ToResponse(await tickets.GetViewAsync(ticketId, cancellationToken)
                                        ?? throw new InvalidOperationException("The saved ticket was not found."));
    }

    /// <summary>Assigning to anyone needs tickets.assign; without it a user may only take the ticket or release their own.</summary>
    private void EnsureAllowed(Ticket ticket, Guid? assigneeId)
    {
        if (currentUser.HasPermission(Permissions.TicketsAssign))
        {
            return;
        }

        var me = currentUser.UserId;
        var takesForSelf = assigneeId is not null && assigneeId == me;
        var releasesOwn = assigneeId is null && ticket.AssigneeId is not null && ticket.AssigneeId == me;
        if (!takesForSelf && !releasesOwn)
        {
            throw new ForbiddenException(TicketText.AssignForbidden);
        }
    }
}
