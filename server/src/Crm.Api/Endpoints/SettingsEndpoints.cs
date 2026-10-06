using Crm.Application.Auth;
using Crm.Application.Settings;

namespace Crm.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // System configuration: SuperAdmin only (settings.manage). Secrets are accepted on PUT but never returned.
        var group = app.MapGroup("/api/settings").RequireAuthorization(Permissions.SettingsManage);

        group.MapGet("", async (ISystemSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.GetAsync(cancellationToken)))
            .WithName("GetSettings");

        group.MapPut("", async (UpdateSettingsRequest request, ISystemSettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(await settings.UpdateAsync(request, cancellationToken)))
            .WithName("UpdateSettings");

        return app;
    }
}
