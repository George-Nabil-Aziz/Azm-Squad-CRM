using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Dashboard;
using Crm.Domain.Chat;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tasks;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Dashboard;

public class SystemOverviewTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Url = "/api/dashboard/system-overview";

    private static async Task<SystemOverviewResponse> OverviewAsync(HttpClient client)
    {
        var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SystemOverviewResponse>())!;
    }

    [Fact]
    public async Task OnlySuperAdminAndAdminMayOpenIt()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateClientWithRoleAsync(Roles.Agent)).GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateClientWithRoleAsync(Roles.Supervisor)).GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateClientWithRoleAsync(Roles.Admin)).GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateAuthenticatedClient(await factory.LoginAsync()).GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Counts_EqualTheDatabase()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var open = (await TicketArrange.TicketAsync(agent, customerId, "System open", null, "mid")).Id;
        var resolved = (await TicketArrange.TicketAsync(agent, customerId, "System resolved", null, "mid")).Id;
        var now = factory.Time.GetUtcNow().UtcDateTime;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == resolved).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Resolved));
            var owner = await db.Users.Select(u => u.Id).FirstAsync();
            db.ChatSessions.Add(ChatSession.Start("Visitor", "v@example.com", "hash", now));
            db.Tasks.Add(WorkTask.Create(owner, "Overdue task", null, now.AddDays(-1), null, now.AddDays(-2)));
            db.Tasks.Add(WorkTask.Create(owner, "Future task", null, now.AddDays(1), null, now));
            db.QuickReplies.Add(QuickReply.Create(owner, "Shared", null, "Body", true, now));
            db.QuickReplies.Add(QuickReply.Create(owner, "Personal", null, "Body", false, now));
            await db.SaveChangesAsync();
        }

        var overview = await OverviewAsync(superAdmin);

        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(await context.Customers.CountAsync(), overview.Customers);
        Assert.Equal(await context.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved), overview.TicketsByStatus.Single(s => s.Key == "resolved").Count);
        Assert.Equal(await context.Tickets.CountAsync(t => t.Status == TicketStatus.New), overview.TicketsByStatus.Single(s => s.Key == "new").Count);
        Assert.Equal(["new", "open", "pending", "resolved", "closed"], overview.TicketsByStatus.Select(s => s.Key));
        Assert.True(overview.TicketsByStatus.Sum(s => s.Count) >= 2);
        Assert.Equal(await context.ChatSessions.CountAsync(c => c.Status == ChatStatus.Waiting), overview.Chats.Waiting);
        Assert.True(overview.Chats.Waiting >= 1);
        Assert.True(overview.Tasks.Overdue >= 1);
        Assert.Equal(await context.Tasks.CountAsync(t => t.CompletedAt == null), overview.Tasks.Open);
        Assert.True(overview.Tasks.Open > overview.Tasks.Overdue);
        Assert.Equal(await context.QuickReplies.CountAsync(q => q.IsShared), overview.QuickReplies.Shared);
        Assert.Equal(await context.QuickReplies.CountAsync(q => !q.IsShared), overview.QuickReplies.Personal);
        Assert.NotEqual(Guid.Empty, open);
    }

    [Fact]
    public async Task Users_AreGroupedByRole_WithAgentsCounted()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        await factory.CreateClientWithRoleAsync(Roles.Agent);

        var overview = await OverviewAsync(superAdmin);

        Assert.Contains(overview.Users.ByRole, r => r.Key == Roles.SuperAdmin && r.Count >= 1);
        Assert.Contains(overview.Users.ByRole, r => r.Key == Roles.Agent && r.Count >= 1);
        Assert.True(overview.Users.ActiveAgents >= 1);
        Assert.InRange(overview.Users.OnDutyAgents, 0, overview.Users.ActiveAgents);
    }

    [Fact]
    public async Task Integrations_AiNotConfigured_MessagesPerChannel_AndRecentActivity()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        await factory.CreateClientWithRoleAsync(Roles.Agent);

        var overview = await OverviewAsync(superAdmin);

        Assert.False(overview.Ai.Configured);
        Assert.Equal(["email", "whatsapp", "sms"], overview.Messages.Select(m => m.Channel));
        Assert.NotNull(overview.RecentActivity);
        Assert.True(overview.RecentActivity!.Count <= 8);
        Assert.Equal(overview.RecentActivity.OrderByDescending(a => a.OccurredAt).Select(a => a.Id), overview.RecentActivity.Select(a => a.Id));
        Assert.True(overview.Webhooks.Total >= overview.Webhooks.Enabled);
        Assert.True(overview.ApiKeys.Total >= overview.ApiKeys.Active);
    }
}
