using Crm.Application.Auth;
using Crm.Application.Dashboard;

namespace Crm.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Counts of every ticket for the staff home dashboard; the customer total is left out without customers.view.
        var group = app.MapGroup("/api/dashboard").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("/overview", async (IDashboardOverviewService overview, CancellationToken cancellationToken) =>
                Results.Ok(await overview.GetAsync(cancellationToken)))
            .WithName("GetDashboardOverview");

        // Every module at a glance: SuperAdmin and Admin only (users.manage).
        app.MapGet("/api/dashboard/system-overview", async (ISystemOverviewService overview, CancellationToken cancellationToken) =>
                Results.Ok(await overview.GetAsync(cancellationToken)))
            .RequireAuthorization(Permissions.UsersManage)
            .WithName("GetSystemOverview");

        return app;
    }
}
