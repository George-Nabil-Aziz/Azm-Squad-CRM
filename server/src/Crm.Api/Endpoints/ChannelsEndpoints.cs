using Crm.Application.Auth;
using Crm.Application.Channels;

namespace Crm.Api.Endpoints;

public static class ChannelsEndpoints
{
    public static IEndpointRouteBuilder MapChannelsEndpoints(this IEndpointRouteBuilder app)
    {
        // Channel settings are an admin area (channels.manage: Admin, SuperAdmin).
        var group = app.MapGroup("/api/channels").RequireAuthorization(Permissions.ChannelsManage);

        group.MapGet("/status", (IChannelSender channels) => Results.Ok(channels.GetStatus()))
            .WithName("GetChannelStatus");

        return app;
    }
}
