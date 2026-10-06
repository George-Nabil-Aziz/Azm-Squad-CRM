using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketsEndpoints
{
    public static IEndpointRouteBuilder MapTicketsEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint needs tickets.view; creating also needs tickets.manage. 401 without a valid token.
        var group = app.MapGroup("/api/tickets").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async ([AsParameters] ListTicketsQuery query, ITicketService tickets,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tickets.ListAsync(query, cancellationToken)))
            .WithName("ListTickets");

        // Staff users tickets can be assigned to: the assignee filter (CRM-14) and the assign picker (CRM-16).
        group.MapGet("/assignees", async (ITicketService tickets, CancellationToken cancellationToken) =>
                Results.Ok(await tickets.ListAssigneesAsync(cancellationToken)))
            .WithName("ListTicketAssignees");

        // The agent dashboard (CRM-29): my tickets by SLA urgency + counters. "mine" is not a guid, so it never clashes with /{id}.
        group.MapGet("/mine", async ([AsParameters] MyTicketsQuery query, IMyTicketsService myTickets,
                    CancellationToken cancellationToken) =>
                Results.Ok(await myTickets.GetAsync(query, cancellationToken)))
            .WithName("ListMyTickets");

        group.MapPost("", async (CreateTicketRequest request, ITicketService tickets, CancellationToken cancellationToken) =>
            {
                var ticket = await tickets.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/tickets/{ticket.Id}", ticket);
            })
            .RequireAuthorization(Permissions.TicketsManage)
            .WithName("CreateTicket");

        group.MapGet("/{id:guid}", async (Guid id, ITicketService tickets, CancellationToken cancellationToken) =>
                Results.Ok(await tickets.GetAsync(id, cancellationToken)))
            .WithName("GetTicket");

        // The customer panel of the ticket page (CRM-30): customer data needs customers.view on top of tickets.view.
        group.MapGet("/{id:guid}/customer-context", async (Guid id, ITicketCustomerContextService context,
                    CancellationToken cancellationToken) =>
                Results.Ok(await context.GetAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersView)
            .WithName("GetTicketCustomerContext");

        return app;
    }
}
