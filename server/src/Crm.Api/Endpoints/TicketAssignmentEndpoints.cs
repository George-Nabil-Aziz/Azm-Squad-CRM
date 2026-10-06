using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapTicketAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        // tickets.manage lets an agent take or release their own ticket; assigning to anyone else needs tickets.assign,
        // which the service checks (403).
        app.MapPost("/api/tickets/{id:guid}/assign", async (Guid id, AssignTicketRequest request,
                    ITicketAssignmentService assignment, CancellationToken cancellationToken) =>
                Results.Ok(await assignment.AssignAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsView, Permissions.TicketsManage)
            .WithName("AssignTicket");

        return app;
    }
}
