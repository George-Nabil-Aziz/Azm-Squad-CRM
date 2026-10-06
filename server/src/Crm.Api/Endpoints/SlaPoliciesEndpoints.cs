using Crm.Application.Auth;
using Crm.Application.Sla;

namespace Crm.Api.Endpoints;

public static class SlaPoliciesEndpoints
{
    public static IEndpointRouteBuilder MapSlaPoliciesEndpoints(this IEndpointRouteBuilder app)
    {
        // SLA settings: SuperAdmin only (sla.manage). 401 without a valid token, 403 for every other role.
        var group = app.MapGroup("/api/sla-policies").RequireAuthorization(Permissions.SlaManage);

        group.MapGet("", async (ISlaPolicyService policies, CancellationToken cancellationToken) =>
                Results.Ok(await policies.ListAsync(cancellationToken)))
            .WithName("ListSlaPolicies");

        group.MapPut("/{priority}", async (string priority, UpdateSlaPolicyRequest request, ISlaPolicyService policies,
                    CancellationToken cancellationToken) =>
                Results.Ok(await policies.UpdateAsync(priority, request, cancellationToken)))
            .WithName("UpdateSlaPolicy");

        return app;
    }
}
