using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-27: automatic assignment on ticket creation, against the real database and API.</summary>
public class AutoAssignmentTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record AgentBody(Guid Id, string FullName, bool OnDuty, int OpenTickets);

    private sealed record SettingsBody(bool AutoAssignEnabled, AgentBody[] Agents);

    private sealed record HistoryBody(string Field, string? OldValue, string? NewValue, Guid? ChangedById);

    private static Task<HttpResponseMessage> SetAutoAssignAsync(HttpClient supervisor, bool enabled) =>
        supervisor.PutAsJsonAsync("/api/settings/assignment", new { autoAssignEnabled = enabled });

    /// <summary>A fresh agent (call <see cref="AllCurrentAgentsOffDutyAsync"/> first, so only the agents of one test compete).</summary>
    private async Task<Guid> AgentAsync(string name, bool active = true, bool onDuty = true)
    {
        var id = await factory.CreateUserAsync($"agent-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(id.ToString()))!;
        user.FullName = name;
        user.IsActive = active;
        user.IsOnDuty = onDuty;
        await users.UpdateAsync(user);
        return id;
    }

    private async Task AllCurrentAgentsOffDutyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var agents = await (from userRole in db.UserRoles
                            join role in db.Roles on userRole.RoleId equals role.Id
                            where role.Name == Roles.Agent
                            select userRole.UserId).ToListAsync();
        await db.Users.Where(u => agents.Contains(u.Id)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsOnDuty, false));
    }

    private async Task GiveOpenTicketAsync(Guid assigneeId, Guid customerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var ticket = Ticket.Create(customerId, "Already assigned", null, null, TicketPriority.Low, TicketChannel.Manual, null, DateTime.UtcNow);
        ticket.AssignNumber(800_000 + await db.Tickets.CountAsync());
        ticket.AssignTo(assigneeId, DateTime.UtcNow);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
    }

    private static Task<TicketBody> CreateTicketAsync(HttpClient client, Guid customerId) =>
        TicketArrange.TicketAsync(client, customerId, priority: "mid");

    [Fact]
    public async Task On_NewTicket_GoesToTheActiveOnDutyAgentWithFewestOpenTickets_AndHistoryIsRecorded()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        await AllCurrentAgentsOffDutyAsync();
        await AgentAsync("Aaa Inactive", active: false);
        await AgentAsync("Aab Off Duty", onDuty: false);
        var busy = await AgentAsync("Zed Busy");
        var free = await AgentAsync("Mmm Free");
        await GiveOpenTicketAsync(busy, customerId);
        Assert.Equal(HttpStatusCode.OK, (await SetAutoAssignAsync(supervisor, true)).StatusCode);

        var ticket = await CreateTicketAsync(supervisor, customerId); // AC 1, 2

        Assert.Equal(free, ticket.AssigneeId);
        Assert.Equal("Mmm Free", ticket.AssigneeName);
        var history = (await supervisor.GetFromJsonAsync<HistoryBody[]>($"/api/tickets/{ticket.Id}/history"))!; // AC 4
        var entry = Assert.Single(history);
        Assert.Equal(("assignee", (string?)null, (string?)"Mmm Free", (Guid?)null), (entry.Field, entry.OldValue, entry.NewValue, entry.ChangedById));

        // Both have one open ticket now: the name decides the tie. Then Mmm Free has two and Zed Busy one.
        Assert.Equal(free, (await CreateTicketAsync(supervisor, customerId)).AssigneeId);
        Assert.Equal(busy, (await CreateTicketAsync(supervisor, customerId)).AssigneeId);
    }

    [Fact]
    public async Task Off_NewTicketStaysUnassigned_AndNoHistoryIsRecorded()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        await AllCurrentAgentsOffDutyAsync();
        await AgentAsync("Any Agent");
        await SetAutoAssignAsync(supervisor, false);

        var ticket = await CreateTicketAsync(supervisor, customerId); // AC 3

        Assert.Null(ticket.AssigneeId);
        Assert.Empty((await supervisor.GetFromJsonAsync<HistoryBody[]>($"/api/tickets/{ticket.Id}/history"))!);
    }

    [Fact]
    public async Task On_NoAvailableAgent_TheTicketStaysUnassigned()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var customerId = await TicketArrange.CustomerAsync(supervisor);
        await AllCurrentAgentsOffDutyAsync();
        await SetAutoAssignAsync(supervisor, true);

        Assert.Null((await CreateTicketAsync(supervisor, customerId)).AssigneeId);
    }

    [Fact]
    public async Task On_ATicketFromAChannel_IsAssignedToo()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        await AllCurrentAgentsOffDutyAsync();
        var free = await AgentAsync("Channel Free");
        await SetAutoAssignAsync(supervisor, true);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInboundMessageProcessor>().ProcessAsync(
            new InboundChannelMessage(ChannelKind.Email, $"<{Guid.NewGuid():N}@mail.example>", $"{Guid.NewGuid():N}@customer.example",
                "Nour", "Printer broken", "It does not print.", factory.Time.GetUtcNow().UtcDateTime),
            CancellationToken.None);

        var ticket = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AsNoTracking().SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal(free, ticket.AssigneeId);
    }

    [Fact]
    public async Task Settings_NeedTicketsAssign_AndListTheAgentsWithTheirDuty()
    {
        var agentClient = await factory.CreateClientWithRoleAsync(Roles.Agent);
        Assert.Equal(HttpStatusCode.Forbidden, (await agentClient.GetAsync("/api/settings/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetAutoAssignAsync(agentClient, true)).StatusCode);

        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await AgentAsync("Duty Agent");
        var response = await supervisor.PutAsJsonAsync($"/api/settings/assignment/agents/{agent}", new { onDuty = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = (await supervisor.GetFromJsonAsync<SettingsBody>("/api/settings/assignment"))!;
        Assert.False(settings.Agents.Single(a => a.Id == agent).OnDuty);
        Assert.Equal(HttpStatusCode.NotFound,
            (await supervisor.PutAsJsonAsync($"/api/settings/assignment/agents/{Guid.NewGuid()}", new { onDuty = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await supervisor.PutAsJsonAsync("/api/settings/assignment", new { })).StatusCode);
    }
}
