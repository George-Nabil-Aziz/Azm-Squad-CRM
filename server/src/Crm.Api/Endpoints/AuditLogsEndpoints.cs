using Crm.Application.Audit;
using Crm.Application.Auth;

namespace Crm.Api.Endpoints;

public static class AuditLogsEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogsEndpoints(this IEndpointRouteBuilder app)
    {
        // The audit log is read-only: only GET exists. 401 without a token, 403 without audit.view (SuperAdmin + Admin).
        var group = app.MapGroup("/api/audit-logs").RequireAuthorization(Permissions.AuditView);

        group.MapGet("", async ([AsParameters] ListAuditLogsQuery query, IAuditLogService logs, CancellationToken cancellationToken) =>
                Results.Ok(await logs.ListAsync(query, cancellationToken)))
            .WithName("ListAuditLogs");

        return app;
    }
}
