using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>GET /api/channels/status: channel settings are an admin area (channels.manage).</summary>
public class ChannelStatusTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record ChannelStateBody(bool Configured);

    public sealed record ChannelStatusBody(ChannelStateBody Email, ChannelStateBody WhatsApp);

    [Fact]
    public async Task Status_AsSuperAdmin_ShowsUnconfiguredChannels()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var status = await superAdmin.GetFromJsonAsync<ChannelStatusBody>("/api/channels/status");

        Assert.False(status!.Email.Configured); // the test host has no SMTP settings
    }

    [Fact]
    public async Task Status_AsAgent_Returns403()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync("/api/channels/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/channels/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
