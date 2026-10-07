using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Dashboard;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Dashboard;

public class DashboardOverviewTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Url = "/api/dashboard/overview";

    private static async Task<DashboardOverviewResponse> OverviewAsync(HttpClient client)
    {
        var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>())!;
    }

    [Fact]
    public async Task Counts_FollowTheTickets_NewTodayOpenPendingResolvedToday()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var before = await OverviewAsync(agent);

        var pending = (await TicketArrange.TicketAsync(agent, customerId, "Overview pending", null, "mid")).Id;
        var resolved = (await TicketArrange.TicketAsync(agent, customerId, "Overview resolved", null, "mid")).Id;
        await TicketArrange.TicketAsync(agent, customerId, "Overview open", null, "mid");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == pending).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Pending));
            await db.Tickets.Where(t => t.Id == resolved).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TicketStatus.Resolved)
                .SetProperty(t => t.ResolvedAt, (DateTime?)factory.Time.GetUtcNow().UtcDateTime));
        }

        var after = await OverviewAsync(agent);

        Assert.Equal(before.NewToday + 3, after.NewToday);
        Assert.Equal(before.OpenTickets + 2, after.OpenTickets);
        Assert.Equal(before.PendingTickets + 1, after.PendingTickets);
        Assert.Equal(before.ResolvedToday + 1, after.ResolvedToday);
    }

    [Fact]
    public async Task Overdue_ListsOpenTicketsPastTheirDueTime_OldestDueFirst_AndCountsThemAsBreachedNow()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var before = await OverviewAsync(agent);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var late = (await TicketArrange.TicketAsync(agent, customerId, "Overview late", null, "high")).Id;
        var notDue = (await TicketArrange.TicketAsync(agent, customerId, "Overview not due", null, "high")).Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == late).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ResponseDueAt, (DateTime?)now.AddYears(-30)).SetProperty(t => t.FirstResponseAt, (DateTime?)null));
            await db.Tickets.Where(t => t.Id == notDue).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ResponseDueAt, (DateTime?)now.AddYears(30)).SetProperty(t => t.ResolutionDueAt, (DateTime?)now.AddYears(30)));
        }

        var after = await OverviewAsync(agent);

        Assert.Equal(before.BreachedNow + 1, after.BreachedNow);
        Assert.Contains(after.Overdue, o => o.TicketId == late);
        Assert.DoesNotContain(after.Overdue, o => o.TicketId == notDue);
        Assert.True(after.Overdue.Count <= 5);
        Assert.Equal(after.Overdue.OrderBy(o => o.DueAt).Select(o => o.TicketId), after.Overdue.Select(o => o.TicketId));
        Assert.All(after.Overdue, o => Assert.StartsWith("TKT-", o.Number));
    }

    [Fact]
    public async Task TotalCustomers_EqualsTheDatabase()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        await TicketArrange.CustomerAsync(agent);

        var overview = await OverviewAsync(agent);

        using var scope = factory.Services.CreateScope();
        var expected = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Customers.CountAsync();
        Assert.True(overview.TotalCustomers >= 1);
        Assert.Equal(expected, overview.TotalCustomers);
    }

    [Fact]
    public async Task NeedsTicketsView_401WithoutToken_EveryStaffRoleMayOpenIt()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
        foreach (var role in new[] { Roles.Agent, Roles.Supervisor, Roles.Admin })
        {
            Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateClientWithRoleAsync(role)).GetAsync(Url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateAuthenticatedClient(await factory.LoginAsync()).GetAsync(Url)).StatusCode);
    }
}
