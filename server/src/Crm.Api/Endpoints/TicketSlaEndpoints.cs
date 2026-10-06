using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

/// <summary>Ticket actions that move the SLA due times (CRM-20).</summary>
public static class TicketSlaEndpoints
{
    public static IEndpointRouteBuilder MapTicketSlaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").RequireAuthorization(Permissions.TicketsView);

        // Changing the priority recalculates ResponseDueAt / ResolutionDueAt (CRM-17 may move this into its own endpoints).
        group.MapPut("/{id:guid}/priority", async (Guid id, ChangeTicketPriorityRequest request, ITicketService tickets,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tickets.ChangePriorityAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsManage)
            .WithName("ChangeTicketPriority");

        return app;
    }
}
