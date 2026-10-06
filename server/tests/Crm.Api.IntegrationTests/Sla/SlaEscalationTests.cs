using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Sla;
using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Sla;

/// <summary>CRM-22: warnings and escalation through the real job class, database and API.</summary>
public class SlaEscalationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<(Guid TicketId, Guid AssigneeId)> AssignedHighTicketAsync()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var created = await TicketArrange.TicketAsync(agent, customerId, priority: "high");
        var assigneeId = await factory.CreateUserAsync($"assignee-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        await UpdateTicketAsync(created.Id, t => typeof(Ticket).GetProperty(nameof(Ticket.AssigneeId))!.SetValue(t, assigneeId)); // CRM-16 assigns later
        return (created.Id, assigneeId);
    }

    private async Task RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitorJob>().RunAsync(CancellationToken.None);
    }

    private async Task UpdateTicketAsync(Guid id, Action<Ticket> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        change(await db.Tickets.SingleAsync(t => t.Id == id));
        await db.SaveChangesAsync();
    }

    private async Task<List<Notification>> NotificationsAsync(Guid ticketId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Notifications.Where(n => n.TicketId == ticketId).ToListAsync();
    }

    private async Task<List<TicketSlaEvent>> EventsAsync(Guid ticketId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().TicketSlaEvents.Where(e => e.TicketId == ticketId).ToListAsync();
    }

    private async Task<EscalationBody> ReadAsync(Guid id) =>
        (await (await factory.CreateClientWithRoleAsync(Roles.Agent)).GetFromJsonAsync<EscalationBody>($"/api/tickets/{id}"))!;

    [Fact]
    public async Task At80PercentOfTheResponseTime_TheAssigneeIsWarnedOnce()
    {
        var (id, assigneeId) = await AssignedHighTicketAsync();

        factory.Time.Advance(TimeSpan.FromMinutes(97)); // High: 120 min response, 80 % = 96 min
        await RunJobAsync();
        await RunJobAsync();

        var notification = Assert.Single(await NotificationsAsync(id));
        Assert.Equal(assigneeId, notification.RecipientUserId);
        Assert.Equal(NotificationType.SlaWarning, notification.Type);
        Assert.Single(await EventsAsync(id), e => e.Type == SlaEventType.ResponseWarning);
        Assert.NotNull((await ReadAsync(id)).ResponseWarnedAt);
    }

    [Fact]
    public async Task OnBreach_TheTicketEscalatesToTheSupervisor_OnlyOncePerLevel()
    {
        var (id, _) = await AssignedHighTicketAsync();
        var supervisorId = await factory.CreateUserAsync($"lead-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Supervisor);

        factory.Time.Advance(TimeSpan.FromMinutes(125));
        await RunJobAsync();
        await RunJobAsync();

        Assert.Equal(1, (await ReadAsync(id)).EscalationLevel);
        var escalation = Assert.Single(await EventsAsync(id), e => e.Type == SlaEventType.Escalated);
        Assert.Equal(1, escalation.Level);
        // The supervisor role is expanded to one notification per active supervisor (CRM-28); each only once.
        var escalations = (await NotificationsAsync(id)).Where(n => n.Type == NotificationType.SlaEscalation).ToList();
        Assert.Single(escalations, n => n.RecipientUserId == supervisorId);
        Assert.Equal(escalations.Count, escalations.Select(n => n.RecipientUserId).Distinct().Count());
    }

    [Fact]
    public async Task ResolvedTicket_IsNotEscalated()
    {
        var (id, _) = await AssignedHighTicketAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(30));
        await UpdateTicketAsync(id, t =>
        {
            t.MarkFirstResponse(factory.Time.GetUtcNow().UtcDateTime);
            t.MarkResolved(factory.Time.GetUtcNow().UtcDateTime);
        });

        factory.Time.Advance(TimeSpan.FromDays(2));
        await RunJobAsync();

        Assert.Equal(0, (await ReadAsync(id)).EscalationLevel);
        Assert.Empty(await NotificationsAsync(id));
        Assert.DoesNotContain(await EventsAsync(id), e => e.Type is SlaEventType.Escalated or SlaEventType.ResponseWarning);
    }

    private sealed record EscalationBody(Guid Id, int EscalationLevel, DateTime? ResponseWarnedAt);
}
