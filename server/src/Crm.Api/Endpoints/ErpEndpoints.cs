using Crm.Application.Auth;
using Crm.Application.Integrations;

namespace Crm.Api.Endpoints;

/// <summary>
/// ERP integration (CRM-60): the link of a customer to its ERP customer, the read-only ERP data for the customer panel and the
/// sync log. Reading needs <c>customers.view</c>, linking <c>customers.manage</c>, the log <c>integrations.manage</c>.
/// </summary>
public static class ErpEndpoints
{
    public static IEndpointRouteBuilder MapErpEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/customers/{id:guid}");

        // Always 200 for a known customer: an unreachable ERP is reported in the body (available = false).
        customers.MapGet("/erp", async (Guid id, IErpService erp, CancellationToken cancellationToken) =>
                Results.Ok(await erp.GetAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersView)
            .WithName("GetCustomerErpData");

        customers.MapPut("/erp-link", async (Guid id, ErpLinkRequest request, IErpService erp, CancellationToken cancellationToken) =>
                Results.Ok(await erp.LinkAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersView, Permissions.CustomersManage)
            .WithName("LinkCustomerToErp");

        app.MapGet("/api/integrations/erp/sync-logs", async (int? page, int? pageSize, IErpService erp, CancellationToken cancellationToken) =>
                Results.Ok(await erp.ListLogsAsync(page, pageSize, cancellationToken)))
            .RequireAuthorization(Permissions.IntegrationsManage)
            .WithName("ListErpSyncLogs");

        return app;
    }
}
