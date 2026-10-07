using Crm.Application.Auth;
using Crm.Application.Departments;
using Crm.Application.Sla;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class DepartmentsEndpoints
{
    public static IEndpointRouteBuilder MapDepartmentsEndpoints(this IEndpointRouteBuilder app)
    {
        // Reading needs tickets.view (the ticket forms and filters use the list); writing departments.manage (admins).
        var group = app.MapGroup("/api/departments").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async ([AsParameters] ListDepartmentsQuery query, IDepartmentService departments,
                    CancellationToken cancellationToken) =>
                Results.Ok(await departments.ListAsync(query, cancellationToken)))
            .WithName("ListDepartments");

        group.MapPost("", async (DepartmentRequest request, IDepartmentService departments, CancellationToken cancellationToken) =>
            {
                var department = await departments.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/departments/{department.Id}", department);
            })
            .RequireAuthorization(Permissions.DepartmentsManage)
            .WithName("CreateDepartment");

        group.MapPut("/{id:guid}", async (Guid id, DepartmentRequest request, IDepartmentService departments,
                    CancellationToken cancellationToken) =>
                Results.Ok(await departments.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.DepartmentsManage)
            .WithName("UpdateDepartment");

        // Per-department SLA overrides (CRM-61 AC 4): SuperAdmin only, like the global SLA policies (sla.manage).
        var sla = app.MapGroup("/api/departments/{id:guid}/sla-policies").RequireAuthorization(Permissions.SlaManage);

        sla.MapGet("", async (Guid id, IDepartmentService departments, CancellationToken cancellationToken) =>
                Results.Ok(await departments.ListSlaPoliciesAsync(id, cancellationToken)))
            .WithName("ListDepartmentSlaPolicies");

        sla.MapPut("/{priority}", async (Guid id, string priority, UpdateSlaPolicyRequest request, IDepartmentService departments,
                    CancellationToken cancellationToken) =>
                Results.Ok(await departments.SetSlaPolicyAsync(id, priority, request, cancellationToken)))
            .WithName("SetDepartmentSlaPolicy");

        sla.MapDelete("/{priority}", async (Guid id, string priority, IDepartmentService departments,
                    CancellationToken cancellationToken) =>
            {
                await departments.RemoveSlaPolicyAsync(id, priority, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RemoveDepartmentSlaPolicy");

        // Transfer a ticket to another department (CRM-61 AC 3); recorded in the ticket history.
        app.MapPut("/api/tickets/{id:guid}/department", async (Guid id, TransferTicketRequest request,
                    ITicketDepartmentService transfer, CancellationToken cancellationToken) =>
                Results.Ok(await transfer.TransferAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsView, Permissions.TicketsManage)
            .WithName("TransferTicketDepartment");

        return app;
    }
}
