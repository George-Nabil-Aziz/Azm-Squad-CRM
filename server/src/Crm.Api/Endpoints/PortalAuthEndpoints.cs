using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Portal;

namespace Crm.Api.Endpoints;

/// <summary>Customer sign-in with an emailed one-time code (CRM-40).</summary>
public static class PortalAuthEndpoints
{
    public static IEndpointRouteBuilder MapPortalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/auth");

        // Anonymous: always 204 for a valid email, known or not (nobody learns which emails are customers).
        group.MapPost("/request-code", async (RequestCodeRequest request, IPortalAuthService auth, CancellationToken cancellationToken) =>
            {
                await auth.RequestCodeAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("PortalRequestCode");

        group.MapPost("/verify", async (VerifyCodeRequest request, IPortalAuthService auth, CancellationToken cancellationToken) =>
                Results.Ok(await auth.VerifyAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithName("PortalVerifyCode");

        // The signed-in customer (only a portal token: a staff token gets 403).
        group.MapGet("/me", async (ClaimsPrincipal user, Crm.Application.Customers.ICustomerRepository customers, CancellationToken cancellationToken) =>
            {
                var id = PortalUser.CustomerId(user);
                var customer = await customers.FindAsync(id, cancellationToken);
                return customer is null
                    ? Results.Unauthorized()
                    : Results.Ok(new PortalCustomerResponse(customer.Id, customer.Name, PortalUser.Email(user)));
            })
            .RequireAuthorization(PortalPolicies.Customer)
            .WithName("PortalMe");

        return app;
    }
}
