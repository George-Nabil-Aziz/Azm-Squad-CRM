using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Sla;

/// <summary>CRM-21: breach detection through the real job class, database and API.</summary>
public class SlaBreachDetectionTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<(HttpClient Agent, Guid TicketId)> HighTicketAsync()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var created = await TicketArrange.TicketAsync(agent, customerId, priority: "high");
        return (agent, created.Id);
    }

    private async Task<SlaMonitorResult> RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SlaMonitorJob>().RunAsync(CancellationToken.None);
    }

    private async Task UpdateTicketAsync(Guid id, Action<Crm.Domain.Tickets.Ticket> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        change(await db.Tickets.SingleAsync(t => t.Id == id));
        await db.SaveChangesAsync();
    }

    private async Task<List<SlaEventType>> EventsAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        return await db.TicketSlaEvents.Where(e => e.TicketId == id && (e.Type == SlaEventType.ResponseBreached || e.Type == SlaEventType.ResolutionBreached))
            .Select(e => e.Type).ToListAsync(); // breach records only; escalation has its own tests (CRM-22)
    }

    // A fresh login each time: the tests move the clock, which expires earlier access tokens.
    private async Task<BreachBody> ReadAsync(Guid id) =>
        (await (await factory.CreateClientWithRoleAsync(Roles.Agent)).GetFromJsonAsync<BreachBody>($"/api/tickets/{id}"))!;

    [Fact]
    public async Task HighTicketWithoutReply_AfterTheResponseTime_IsResponseBreached()
    {
        var (_, id) = await HighTicketAsync();
        Assert.False((await ReadAsync(id)).ResponseBreached);

        factory.Time.Advance(TimeSpan.FromMinutes(121)); // High: 2 h response
        await RunJobAsync();

        var ticket = await ReadAsync(id);
        Assert.True(ticket.ResponseBreached);
        Assert.False(ticket.ResolutionBreached);
        Assert.Equal([SlaEventType.ResponseBreached], await EventsAsync(id));
    }

    [Fact]
    public async Task TicketRepliedInTime_IsNotBreached()
    {
        var (_, id) = await HighTicketAsync();
        await UpdateTicketAsync(id, t => t.MarkFirstResponse(factory.Time.GetUtcNow().UtcDateTime.AddMinutes(10)));

        factory.Time.Advance(TimeSpan.FromMinutes(130));
        await RunJobAsync();

        Assert.False((await ReadAsync(id)).ResponseBreached);
        Assert.DoesNotContain(SlaEventType.ResponseBreached, await EventsAsync(id));
    }

    [Fact]
    public async Task FirstAgentReplyThroughTheApi_IsSeenBySla_AndPreventsAResponseBreach()
    {
        var (agent, id) = await HighTicketAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));
        var reply = await agent.PostAsJsonAsync($"/api/tickets/{id}/messages", new { body = "We are on it." });
        Assert.True(reply.IsSuccessStatusCode);

        factory.Time.Advance(TimeSpan.FromMinutes(200)); // far past the 2 h response time
        await RunJobAsync();

        var ticket = await ReadAsync(id);
        Assert.NotNull(ticket.FirstResponseAt); // CRM-15 sets the same field SLA reads
        Assert.False(ticket.ResponseBreached);
        Assert.DoesNotContain(SlaEventType.ResponseBreached, await EventsAsync(id));
    }

    [Fact]
    public async Task TicketResolvedAfterResolutionDue_IsResolutionBreached()
    {
        var (_, id) = await HighTicketAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(500)); // High: 8 h resolution
        await UpdateTicketAsync(id, t =>
        {
            t.MarkFirstResponse(factory.Time.GetUtcNow().UtcDateTime.AddMinutes(-450));
            t.MarkResolved(factory.Time.GetUtcNow().UtcDateTime);
        });

        await RunJobAsync();

        var ticket = await ReadAsync(id);
        Assert.True(ticket.ResolutionBreached);
        Assert.Equal([SlaEventType.ResolutionBreached], await EventsAsync(id));
    }

    [Fact]
    public async Task RunningTheJobTwice_DoesNotDuplicateBreachRecords()
    {
        var (_, id) = await HighTicketAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(600));

        await RunJobAsync();
        var second = await RunJobAsync();

        Assert.Equal(new SlaMonitorResult(0, 0), second);
        Assert.Equal(2, (await EventsAsync(id)).Count);
    }

    private sealed record BreachBody(Guid Id, bool ResponseBreached, bool ResolutionBreached, DateTime? FirstResponseAt);
}
