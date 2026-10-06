using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Reports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Reports;

public class DashboardTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<DashboardResponse> DashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/reports/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>())!;
    }

    private async Task<Guid> NewTicketAsync(HttpClient agent, Guid customerId, string priority = "mid") =>
        (await TicketArrange.TicketAsync(agent, customerId, "Dashboard seed", null, priority)).Id;

    [Fact]
    public async Task OpenTickets_EqualsTheDatabase_AndFollowsStatusChanges()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var before = await DashboardAsync(supervisor);

        var id = await NewTicketAsync(agent, customerId);
        await NewTicketAsync(agent, customerId);
        var added = await DashboardAsync(supervisor);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Resolved));
        }

        var resolved = await DashboardAsync(supervisor);
        using var check = factory.Services.CreateScope();
        var expected = await check.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets
            .CountAsync(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

        Assert.Equal(before.OpenTickets + 2, added.OpenTickets);
        Assert.Equal(added.OpenTickets - 1, resolved.OpenTickets);
        Assert.Equal(expected, resolved.OpenTickets);
    }

    [Fact]
    public async Task BreachedToday_CountsTicketsWhoseDueTimePassedToday()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var before = await DashboardAsync(supervisor);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var dueEarlierToday = now.Date.AddTicks((now - now.Date).Ticks / 2); // between 00:00 and now, still today

        var breached = await NewTicketAsync(agent, customerId);
        var notYetDue = await NewTicketAsync(agent, customerId);
        var dueYesterday = await NewTicketAsync(agent, customerId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == breached).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ResponseDueAt, (DateTime?)dueEarlierToday).SetProperty(t => t.FirstResponseAt, (DateTime?)null));
            await db.Tickets.Where(t => t.Id == notYetDue).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ResponseDueAt, (DateTime?)new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            await db.Tickets.Where(t => t.Id == dueYesterday).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ResponseDueAt, (DateTime?)now.Date.AddHours(-5)));
        }

        var after = await DashboardAsync(supervisor);

        Assert.Equal(before.BreachedToday + 1, after.BreachedToday);
    }

    [Fact]
    public async Task Charts_ListTheLast14Days_AndEveryChannel_IncludingTodaysTickets()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        await NewTicketAsync(agent, customerId);

        var dashboard = await DashboardAsync(supervisor);

        Assert.Equal(14, dashboard.TicketsPerDay.Count);
        Assert.Equal(DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime), dashboard.TicketsPerDay[^1].Date);
        Assert.True(dashboard.TicketsPerDay[^1].Count >= 1);
        Assert.Equal(["manual", "email", "whatsapp", "portal", "webform"], dashboard.TicketsByChannel.Select(c => c.Key));
        Assert.True(dashboard.TicketsByChannel.Single(c => c.Key == "manual").Count >= 1);
        Assert.Equal(dashboard.TicketsPerDay.Sum(d => d.Count), dashboard.TicketsByChannel.Sum(c => c.Count));
        Assert.Null(dashboard.AverageCsat); // CRM-44 is not wired
        Assert.Equal(DateTimeKind.Utc, dashboard.GeneratedAt.Kind);
    }

    [Fact]
    public async Task OnlySupervisorAdminAndSuperAdmin_CanOpenIt()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync("/api/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/reports/dashboard")).StatusCode);
    }
}
