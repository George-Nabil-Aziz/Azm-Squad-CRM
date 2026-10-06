using Crm.Application.Auth;
using Crm.Application.Reports;

namespace Crm.Api.Endpoints;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        // Management reports: SuperAdmin, Admin and Supervisor (reports.view). 401 without a token, 403 for an Agent.
        var group = app.MapGroup("/api/reports").RequireAuthorization(Permissions.ReportsView);

        group.MapGet("/tickets", async ([AsParameters] TicketReportQuery query, ITicketReportService reports, CancellationToken cancellationToken) =>
                Results.Ok(await reports.GetAsync(query, cancellationToken)))
            .WithName("GetTicketReport");

        group.MapGet("/tickets/export", async ([AsParameters] TicketReportQuery query, string? format, ITicketReportService reports,
                    CancellationToken cancellationToken) =>
                ToFile(await reports.ExportAsync(query, format, cancellationToken)))
            .WithName("ExportTicketReport");

        group.MapGet("/sla", async ([AsParameters] SlaQuery query, ISlaReportService reports, CancellationToken cancellationToken) =>
                Results.Ok(await reports.GetAsync(query, cancellationToken)))
            .WithName("GetSlaReport");

        group.MapGet("/sla/breaches", async ([AsParameters] SlaBreachesQuery query, ISlaReportService reports, CancellationToken cancellationToken) =>
                Results.Ok(await reports.ListBreachesAsync(query, cancellationToken)))
            .WithName("ListSlaBreaches");

        group.MapGet("/csat", async ([AsParameters] CsatQuery query, ICsatReportService reports, CancellationToken cancellationToken) =>
                Results.Ok(await reports.GetAsync(query, cancellationToken)))
            .WithName("GetCsatReport");

        group.MapGet("/agents", async ([AsParameters] AgentQuery query, IAgentReportService reports, CancellationToken cancellationToken) =>
                Results.Ok(await reports.GetAsync(query, cancellationToken)))
            .WithName("GetAgentReport");

        group.MapGet("/agents/export", async ([AsParameters] AgentQuery query, string? format, IAgentReportService reports,
                    CancellationToken cancellationToken) =>
                ToFile(await reports.ExportAsync(query, format, cancellationToken)))
            .WithName("ExportAgentReport");

        group.MapGet("/dashboard", async (IDashboardService dashboard, CancellationToken cancellationToken) =>
                Results.Ok(await dashboard.GetAsync(cancellationToken)))
            .WithName("GetManagementDashboard");

        return app;
    }

    private static IResult ToFile(ReportFile file) => Results.File(file.Content, file.ContentType, file.FileName);
}
