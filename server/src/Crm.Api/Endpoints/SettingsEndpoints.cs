using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // Automatic assignment (CRM-27): supervisors and admins (tickets.assign).
        var group = app.MapGroup("/api/settings/assignment").RequireAuthorization(Permissions.TicketsAssign);

        group.MapGet("/", async (IAssignmentSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.GetAsync(cancellationToken)))
            .WithName("GetAssignmentSettings");

        group.MapPut("/", async (UpdateAssignmentSettingsRequest request, IAssignmentSettingsService settings,
                    CancellationToken cancellationToken) =>
                Results.Ok(await settings.UpdateAsync(request, cancellationToken)))
            .WithName("UpdateAssignmentSettings");

        group.MapPut("/agents/{userId:guid}", async (Guid userId, SetAgentDutyRequest request,
                    IAssignmentSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.SetOnDutyAsync(userId, request, cancellationToken)))
            .WithName("SetAgentDuty");

        return app;
    }
}
