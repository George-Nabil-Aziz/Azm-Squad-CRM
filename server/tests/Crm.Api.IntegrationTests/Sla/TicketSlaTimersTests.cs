using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Sla;

/// <summary>CRM-20: SLA due times on tickets.</summary>
public class TicketSlaTimersTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<HttpClient> SuperAdminAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    private static async Task<TicketSlaBody> ReadAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TicketSlaBody>($"/api/tickets/{id}"))!;

    private static async Task<SlaPolicyBody> PolicyAsync(HttpClient superAdmin, string priority) =>
        (await superAdmin.GetFromJsonAsync<SlaPolicyBody[]>("/api/sla-policies"))!.Single(p => p.Priority == priority);

    [Fact]
    public async Task CreateTicket_GetsDueTimesFromThePolicyOfItsPriority()
    {
        var superAdmin = await SuperAdminAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var policy = await PolicyAsync(superAdmin, "high");
        var customerId = await TicketArrange.CustomerAsync(agent);

        var created = await TicketArrange.TicketAsync(agent, customerId, priority: "high");
        var ticket = await ReadAsync(agent, created.Id);

        Assert.Equal(ticket.CreatedAt.AddMinutes(policy.ResponseMinutes), ticket.ResponseDueAt);
        Assert.Equal(ticket.CreatedAt.AddMinutes(policy.ResolutionMinutes), ticket.ResolutionDueAt);
        Assert.Equal(DateTimeKind.Utc, ticket.ResponseDueAt!.Value.Kind);
        Assert.Null(ticket.FirstResponseAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public async Task ChangingThePriority_RecalculatesTheDueTimes()
    {
        var superAdmin = await SuperAdminAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var high = await PolicyAsync(superAdmin, "high");
        var customerId = await TicketArrange.CustomerAsync(agent);
        var created = await TicketArrange.TicketAsync(agent, customerId, priority: "low");

        var response = await agent.PutAsJsonAsync($"/api/tickets/{created.Id}/priority", new { priority = "high" });
        var changed = await response.Content.ReadFromJsonAsync<TicketSlaBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("high", changed!.Priority);
        Assert.Equal(changed.CreatedAt.AddMinutes(high.ResponseMinutes), changed.ResponseDueAt);
        Assert.Equal(changed.CreatedAt.AddMinutes(high.ResolutionMinutes), changed.ResolutionDueAt);
        Assert.Equal(changed.ResponseDueAt, (await ReadAsync(agent, created.Id)).ResponseDueAt);
    }

    [Fact]
    public async Task ChangingThePolicy_DoesNotMoveExistingDueTimes()
    {
        var superAdmin = await SuperAdminAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var mid = await PolicyAsync(superAdmin, "mid");
        var customerId = await TicketArrange.CustomerAsync(agent);
        var created = await TicketArrange.TicketAsync(agent, customerId, priority: "mid");
        var before = await ReadAsync(agent, created.Id);

        var update = await superAdmin.PutAsJsonAsync("/api/sla-policies/mid", new { responseMinutes = 5, resolutionMinutes = 10 });
        var after = await ReadAsync(agent, created.Id);
        await superAdmin.PutAsJsonAsync("/api/sla-policies/mid", new { mid.ResponseMinutes, mid.ResolutionMinutes });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(before.ResponseDueAt, after.ResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData(null)]
    public async Task ChangePriority_Invalid_Returns400(string? priority)
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var created = await TicketArrange.TicketAsync(agent, customerId);

        var response = await agent.PutAsJsonAsync($"/api/tickets/{created.Id}/priority", new { priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["priority"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task ChangePriority_UnknownTicket_Returns404()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.PutAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/priority", new { priority = "high" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangePriority_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/priority", new { priority = "high" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ChangePriority_NeedsTicketsManage()
    {
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/tickets/{id:guid}/priority")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy!).Order().ToArray();

        Assert.Equal([Permissions.TicketsManage, Permissions.TicketsView], policies);
    }
}
