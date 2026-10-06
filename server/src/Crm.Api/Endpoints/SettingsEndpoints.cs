using Crm.Application.Auth;
using Crm.Application.Settings;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // Automatic assignment (CRM-27): supervisors and admins (tickets.assign).
        var assignment = app.MapGroup("/api/settings/assignment").RequireAuthorization(Permissions.TicketsAssign);

        assignment.MapGet("/", async (IAssignmentSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.GetAsync(cancellationToken)))
            .WithName("GetAssignmentSettings");

        assignment.MapPut("/", async (UpdateAssignmentSettingsRequest request, IAssignmentSettingsService settings,
                    CancellationToken cancellationToken) =>
                Results.Ok(await settings.UpdateAsync(request, cancellationToken)))
            .WithName("UpdateAssignmentSettings");

        assignment.MapPut("/agents/{userId:guid}", async (Guid userId, SetAgentDutyRequest request,
                    IAssignmentSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.SetOnDutyAsync(userId, request, cancellationToken)))
            .WithName("SetAgentDuty");

        // System configuration (CRM-35): SuperAdmin only (settings.manage). Secrets are accepted on PUT but never returned.
        var system = app.MapGroup("/api/settings").RequireAuthorization(Permissions.SettingsManage);

        system.MapGet("", async (ISystemSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.GetAsync(cancellationToken)))
            .WithName("GetSettings");

        system.MapPut("", async (UpdateSettingsRequest request, ISystemSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.UpdateAsync(request, cancellationToken)))
            .WithName("UpdateSettings");

        return app;
    }
}
