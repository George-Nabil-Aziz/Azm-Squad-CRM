using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-17: PUT /api/tickets/{id}/status.</summary>
public class TicketStatusTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid ticketId, string status) =>
        client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status });

    private static async Task<TicketBody> MoveOkAsync(HttpClient client, Guid ticketId, string status)
    {
        var response = await MoveAsync(client, ticketId, status);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketBody>())!;
    }

    private async Task<(HttpClient Agent, TicketBody Ticket)> ArrangeAsync()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        return (agent, await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent)));
    }

    private async Task<List<TicketHistoryEntry>> HistoryAsync(Guid ticketId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().TicketHistory
            .AsNoTracking().Where(h => h.TicketId == ticketId).OrderBy(h => h.Id).ToListAsync();
    }

    [Fact]
    public async Task WalkingTheWholeWorkflow_Succeeds_AndWritesAHistoryEntryPerStep()
    {
        var (agent, ticket) = await ArrangeAsync();
        Assert.Equal("new", ticket.Status);
        string[] steps = ["open", "pending", "open", "resolved", "closed", "open"];

        foreach (var step in steps)
        {
            Assert.Equal(step, (await MoveOkAsync(agent, ticket.Id, step)).Status);
        }

        var history = await HistoryAsync(ticket.Id);
        Assert.Equal(steps.Length, history.Count);
        Assert.All(history, h => Assert.Equal(TicketHistoryField.Status, h.Field));
        Assert.Equal(["new", "open", "pending", "open", "resolved", "closed"], history.Select(h => h.OldValue));
        Assert.Equal(steps, history.Select(h => h.NewValue));
        Assert.All(history, h => Assert.NotNull(h.ChangedById));
    }

    [Fact]
    public async Task ClosedToPending_Returns400_OnStatus_AndNothingChanges()
    {
        var (agent, ticket) = await ArrangeAsync();
        foreach (var step in new[] { "open", "resolved", "closed" })
        {
            await MoveOkAsync(agent, ticket.Id, step);
        }

        var response = await MoveAsync(agent, ticket.Id, "pending");
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        var reloaded = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("status", problem!.Errors.Keys);
        Assert.Equal("closed", reloaded!.Status);
        Assert.Equal(3, (await HistoryAsync(ticket.Id)).Count);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("")]
    [InlineData("new")]
    public async Task InvalidOrIllegalTargets_Return400(string status)
    {
        var (agent, ticket) = await ArrangeAsync();
        await MoveOkAsync(agent, ticket.Id, "open");

        var response = await MoveAsync(agent, ticket.Id, status);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resolving_SetsResolvedAt_AndReopeningClearsIt()
    {
        var (agent, ticket) = await ArrangeAsync();
        await MoveOkAsync(agent, ticket.Id, "open");
        Assert.Null((await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}"))!.ResolvedAt);

        var resolved = await MoveOkAsync(agent, ticket.Id, "resolved");
        var stored = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");
        var reopened = await MoveOkAsync(agent, ticket.Id, "open");

        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal(DateTimeKind.Utc, resolved.ResolvedAt.Value.Kind);
        Assert.Equal(resolved.ResolvedAt, stored!.ResolvedAt);
        Assert.Null(reopened.ResolvedAt);
        Assert.Null((await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}"))!.ResolvedAt);
    }

    [Fact]
    public async Task TicketResponse_ListsTheAllowedStatuses()
    {
        var (agent, ticket) = await ArrangeAsync();

        var open = await MoveOkAsync(agent, ticket.Id, "open");

        Assert.Equal(["pending", "resolved"], open.AllowedStatuses!);
        Assert.Equal(["pending", "resolved"], (await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}"))!.AllowedStatuses!);
    }

    [Fact]
    public async Task ReopeningAClosedTicket_LetsItTakeRepliesAgain()
    {
        var (agent, ticket) = await ArrangeAsync();
        foreach (var step in new[] { "open", "resolved", "closed" })
        {
            await MoveOkAsync(agent, ticket.Id, step);
        }

        var whileClosed = await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "Hello?" });
        await MoveOkAsync(agent, ticket.Id, "open");
        var afterReopen = await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "Hello?" });

        Assert.Equal(HttpStatusCode.BadRequest, whileClosed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterReopen.StatusCode);
    }

    [Fact]
    public async Task UnknownTicket_Returns404()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await MoveAsync(agent, Guid.NewGuid(), "open")).StatusCode);
    }
}
