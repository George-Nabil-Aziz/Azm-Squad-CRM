using Crm.Application.Auth;
using Crm.Application.Customers;

namespace Crm.Api.Endpoints;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint needs customers.view; the write endpoints also need customers.manage
        // (several RequireAuthorization calls combine with AND). 401 without a valid token.
        var group = app.MapGroup("/api/customers").RequireAuthorization(Permissions.CustomersView);

        group.MapGet("", async ([AsParameters] ListCustomersQuery query, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.ListAsync(query, cancellationToken)))
            .WithName("ListCustomers");

        group.MapGet("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
                Results.Ok(await customers.GetAsync(id, cancellationToken)))
            .WithName("GetCustomer");

        group.MapPost("", async (CustomerRequest request, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                var customer = await customers.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/customers/{customer.Id}", customer);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("CreateCustomer");

        group.MapPut("/{id:guid}", async (Guid id, CustomerRequest request, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("UpdateCustomer");

        group.MapDelete("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("DeleteCustomer");

        return app;
    }
}
