using Crm.Application.Common.Exceptions;
using Crm.Application.Departments;
using Crm.Domain.Departments;
using Crm.Domain.Tickets;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>Body of PUT /api/tickets/{id}/department: the target department (must be active); null = general.</summary>
public sealed record TransferTicketRequest(Guid? DepartmentId);

/// <summary>The department a ticket is in after a transfer (the ticket itself may no longer be visible to the caller).</summary>
public sealed record TicketDepartmentResponse(Guid TicketId, Guid? DepartmentId, string? DepartmentName);

/// <summary>Transfers tickets between departments (CRM-61 AC 3). Failures: 404 unknown ticket, 400 unknown / inactive department.</summary>
public interface ITicketDepartmentService
{
    Task<TicketDepartmentResponse> TransferAsync(Guid ticketId, TransferTicketRequest request, CancellationToken cancellationToken);
}

public sealed class TicketDepartmentService(
    ITicketRepository tickets,
    IDepartmentRepository departments,
    ITicketHistoryRecorder history,
    TimeProvider timeProvider) : ITicketDepartmentService
{
    public async Task<TicketDepartmentResponse> TransferAsync(
        Guid ticketId, TransferTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);

        Department? target = null;
        if (request.DepartmentId is { } targetId)
        {
            target = await departments.FindAsync(targetId, cancellationToken);
            if (target is not { IsActive: true })
            {
                throw new ValidationException(new Dictionary<string, string[]> { ["departmentId"] = [DepartmentText.Unavailable] });
            }
        }

        if (ticket.DepartmentId == request.DepartmentId)
        {
            return new TicketDepartmentResponse(ticket.Id, ticket.DepartmentId, target?.Name); // nothing to change, nothing to record
        }

        var oldName = ticket.DepartmentId is { } oldId ? (await departments.FindAsync(oldId, cancellationToken))?.Name : null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        ticket.ChangeDepartment(request.DepartmentId, now);
        history.Record(ticket.Id, TicketHistoryField.Department, oldName, target?.Name, now);
        await tickets.SaveChangesAsync(cancellationToken);
        return new TicketDepartmentResponse(ticket.Id, ticket.DepartmentId, target?.Name);
    }
}
