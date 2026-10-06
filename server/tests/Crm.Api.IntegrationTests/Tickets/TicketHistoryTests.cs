using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Sla;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-18: the ticket audit trail (GET /api/tickets/{id}/history) and the category change that feeds it.</summary>
public class TicketHistoryTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record HistoryBody(
        string Id, string Field, string? OldValue, string? NewValue, Guid? ChangedById, string? ChangedByName, DateTime ChangedAt);

    private sealed record MeBody(Guid Id, string FullName);

    private static Task<HistoryBody[]?> HistoryAsync(HttpClient client, Guid ticketId) =>
        client.GetFromJsonAsync<HistoryBody[]>($"/api/tickets/{ticketId}/history");

    [Fact]
    public async Task StatusAssigneePriorityAndCategoryChanges_AreRecorded_OldestFirst_WithUserAndTime()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var me = (await supervisor.GetFromJsonAsync<MeBody>("/api/auth/me"))!;
        var admin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var billing = await TicketArrange.CategoryAsync(admin, "Billing");
        var ticket = await TicketArrange.TicketAsync(supervisor, customerId, "Invoice is wrong", billing.Id, "low");
        var support = await TicketArrange.CategoryAsync(admin, "Support");

        Assert.Empty((await HistoryAsync(supervisor, ticket.Id))!);

        factory.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.PutAsJsonAsync($"/api/tickets/{ticket.Id}/status", new { status = "open" })).StatusCode);
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId = me.Id })).StatusCode);
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.PutAsJsonAsync($"/api/tickets/{ticket.Id}/priority", new { priority = "high" })).StatusCode);
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.PutAsJsonAsync($"/api/tickets/{ticket.Id}/category", new { categoryId = support.Id })).StatusCode);

        var history = (await HistoryAsync(supervisor, ticket.Id))!;

        Assert.Equal(["status", "assignee", "priority", "category"], history.Select(h => h.Field));
        Assert.Equal([("new", "open"), (null, me.FullName), ("low", "high"), (billing.Name, support.Name)],
            history.Select(h => (h.OldValue, h.NewValue)));
        Assert.All(history, h => Assert.Equal((me.Id, me.FullName), (h.ChangedById!.Value, h.ChangedByName)));
        Assert.Equal(history.OrderBy(h => h.ChangedAt), history);
        Assert.Equal(4, history.Select(h => h.ChangedAt).Distinct().Count());
        Assert.All(history, h => Assert.Equal(DateTimeKind.Utc, h.ChangedAt.Kind));
    }

    [Fact]
    public async Task ChangingNothing_RecordsNothing()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var ticket = await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), priority: "high");

        await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/priority", new { priority = "high" });
        await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/category", new { categoryId = (Guid?)null });

        Assert.Empty((await HistoryAsync(agent, ticket.Id))!);
    }

    [Fact]
    public async Task ChangingToAnInactiveCategory_Returns400()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var ticket = await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent));

        var response = await agent.PutAsJsonAsync($"/api/tickets/{ticket.Id}/category", new { categoryId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await HistoryAsync(agent, ticket.Id))!);
    }

    [Fact]
    public async Task AnSlaEscalation_IsShownInTheHistory_WithItsLevel_AndNoUser()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var ticket = await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), priority: "high");

        factory.Time.Advance(TimeSpan.FromMinutes(121)); // High: 120 minutes to respond
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SlaMonitorJob>().RunAsync(CancellationToken.None);
        }

        var reader = await factory.CreateClientWithRoleAsync(Roles.Agent); // the first token has expired by now
        var history = (await HistoryAsync(reader, ticket.Id))!;

        var escalation = Assert.Single(history);
        Assert.Equal(("escalation", "1", (Guid?)null, (string?)null), (escalation.Field, escalation.NewValue, escalation.ChangedById, escalation.ChangedByName));
    }

    [Fact]
    public async Task History_IsReadOnly_NoWriteRoutesExist()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var ticket = await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent));
        var url = $"/api/tickets/{ticket.Id}/history";

        var attempts = new[]
        {
            await agent.PostAsJsonAsync(url, new { field = "status" }),
            await agent.PutAsJsonAsync(url, new { }),
            await agent.PatchAsJsonAsync(url, new { }),
            await agent.DeleteAsync(url),
            await agent.PutAsJsonAsync($"{url}/h1", new { }),
            await agent.DeleteAsync($"{url}/h1"),
        };

        Assert.All(attempts, r => Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, r.StatusCode.ToString()));
        var methods = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("/history", StringComparison.Ordinal) == true)
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods).ToArray();
        Assert.Equal(["GET"], methods);
    }

    [Fact]
    public async Task UnknownTicket_Returns404()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await agent.PutAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/category", new { categoryId = (Guid?)null })).StatusCode);
    }
}
