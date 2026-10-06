using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-29: the agent dashboard endpoint against the real database.</summary>
public class MyTicketsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record CountersBody(int Open, int Pending, int BreachedToday);

    private sealed record ItemBody(Guid Id, string Number, string Priority, string Status, Guid? AssigneeId);

    private sealed record PageBody(ItemBody[] Items, int Page, int PageSize, int TotalCount);

    private sealed record MineBody(CountersBody Counters, PageBody Tickets);

    private async Task<(Guid Id, string Email, HttpClient Client)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (id, email, factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)));
    }

    private async Task<TicketBody> TicketForAsync(HttpClient supervisor, Guid customerId, Guid assignee, string priority)
    {
        var ticket = await TicketArrange.TicketAsync(supervisor, customerId, priority: priority);
        var response = await supervisor.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assigneeId = assignee });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ticket;
    }

    private async Task SetStatusAsync(Guid ticketId, TicketStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnsOnlyMyNotClosedTickets_NearestSlaDueFirst_WithCounters()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var me = await AgentAsync();
        var other = await AgentAsync();
        var low = await TicketForAsync(supervisor, customerId, me.Id, "low"); // response due in 8 h
        var high = await TicketForAsync(supervisor, customerId, me.Id, "high"); // response due in 2 h
        var closed = await TicketForAsync(supervisor, customerId, me.Id, "high");
        var pending = await TicketForAsync(supervisor, customerId, me.Id, "mid");
        await TicketForAsync(supervisor, customerId, other.Id, "high");
        await SetStatusAsync(closed.Id, TicketStatus.Closed);
        await SetStatusAsync(pending.Id, TicketStatus.Pending);

        var mine = (await me.Client.GetFromJsonAsync<MineBody>("/api/tickets/mine"))!;

        // AC 1 and 2: High (2 h) before Mid (4 h, pending) before Low (8 h); the closed and the other agent's tickets are absent.
        Assert.Equal([high.Id, pending.Id, low.Id], mine.Tickets.Items.Select(t => t.Id));
        Assert.All(mine.Tickets.Items, t => Assert.Equal(me.Id, t.AssigneeId));
        Assert.Equal(3, mine.Tickets.TotalCount);
        Assert.Equal(new CountersBody(2, 1, 0), mine.Counters); // AC 3
    }

    [Fact]
    public async Task BreachedToday_CountsMyTicketsWhoseSlaWasBreachedToday()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        var me = await AgentAsync();
        await TicketForAsync(supervisor, customerId, me.Id, "high");
        await TicketForAsync(supervisor, customerId, me.Id, "low");
        factory.Time.Advance(TimeSpan.FromMinutes(125)); // the High response (2 h) is late, the Low one (8 h) is not
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SlaMonitorJob>().RunAsync(CancellationToken.None);
        }

        var fresh = factory.CreateAuthenticatedClient(await factory.LoginAsync(me.Email, CrmApiFactory.TestUserPassword));
        var mine = (await fresh.GetFromJsonAsync<MineBody>("/api/tickets/mine"))!;

        Assert.Equal(1, mine.Counters.BreachedToday);
    }

    [Fact]
    public async Task NothingAssigned_ReturnsAnEmptyList()
    {
        var me = await AgentAsync();

        var mine = (await me.Client.GetFromJsonAsync<MineBody>("/api/tickets/mine"))!;

        Assert.Empty(mine.Tickets.Items);
        Assert.Equal(new CountersBody(0, 0, 0), mine.Counters);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/tickets/mine")).StatusCode);
}
