using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Common.Paging;
using Crm.Application.Reports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Reports;

public class SlaReportTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>, IAsyncLifetime
{
    private const string From = "2025-04-01";
    private const string To = "2025-04-05";
    // Far in the future: a ticket due then is still pending; every other seeded due time (2025) is long past ("now" is 2026 or later).
    private static readonly DateTime Future = new(2099, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private HttpClient _supervisor = null!;
    private HttpClient _agent = null!;

    private static DateTime At(int day, int hour, int minute = 0) => new(2025, 4, day, hour, minute, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        _agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        _supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        if (await SeededAsync())
        {
            return;
        }

        var customerId = await TicketArrange.CustomerAsync(_agent);

        // (subject, priority, created, response due, first response, resolution due, resolved)
        (string Subject, TicketPriority Priority, DateTime Created, DateTime? RespDue, DateTime? First, DateTime? ResDue, DateTime? Resolved)[] seeds =
        [
            ("A met", TicketPriority.High, At(1, 9), At(1, 11), At(1, 10), At(1, 17), At(1, 16)),
            ("B response breached", TicketPriority.High, At(1, 10), At(1, 12), At(1, 13), At(1, 18), null),
            ("C pending", TicketPriority.High, At(2, 9), Future, null, Future.AddHours(8), null),
            ("D resolution breached", TicketPriority.Mid, At(3, 9), At(3, 13), At(3, 10), At(4, 9), At(4, 15)),
            ("E no sla", TicketPriority.Low, At(4, 9), null, At(4, 9, 30), null, null),
            ("F before the range", TicketPriority.High, new DateTime(2025, 3, 31, 23, 59, 59, DateTimeKind.Utc), At(1, 0), null, At(1, 0), null),
            ("G after the range", TicketPriority.High, new DateTime(2025, 4, 6, 0, 0, 0, DateTimeKind.Utc), At(6, 1), null, At(6, 1), null),
        ];

        foreach (var seed in seeds)
        {
            var created = await TicketArrange.TicketAsync(_agent, customerId, "SLA seed " + seed.Subject, null, "mid");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == created.Id).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Priority, seed.Priority)
                .SetProperty(t => t.CreatedAt, seed.Created)
                .SetProperty(t => t.ResponseDueAt, seed.RespDue)
                .SetProperty(t => t.FirstResponseAt, seed.First)
                .SetProperty(t => t.ResolutionDueAt, seed.ResDue)
                .SetProperty(t => t.ResolvedAt, seed.Resolved));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> SeededAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AnyAsync(t => t.Subject.StartsWith("SLA seed "));
    }

    private async Task<SlaReportResponse> ReportAsync(string query = "")
    {
        var response = await _supervisor.GetAsync($"/api/reports/sla?from={From}&to={To}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SlaReportResponse>())!;
    }

    [Fact]
    public async Task ComplianceAndCounts_PerPriority_AreRight()
    {
        var report = await ReportAsync();

        var high = report.Priorities.Single(p => p.Priority == "high");
        Assert.Equal(3, high.Tickets);
        Assert.Equal((1, 1, 1), (high.Response.Met, high.Response.Breached, high.Response.Pending));
        Assert.Equal(50.0, high.Response.CompliancePercent);
        Assert.Equal((1, 1, 1), (high.Resolution.Met, high.Resolution.Breached, high.Resolution.Pending));
        Assert.Equal(50.0, high.Resolution.CompliancePercent);

        var mid = report.Priorities.Single(p => p.Priority == "mid");
        Assert.Equal(1, mid.Tickets);
        Assert.Equal(100.0, mid.Response.CompliancePercent);
        Assert.Equal(0.0, mid.Resolution.CompliancePercent);

        var low = report.Priorities.Single(p => p.Priority == "low");
        Assert.Equal(1, low.Tickets); // counted, but it has no SLA times
        Assert.Equal((0, 0, 0), (low.Response.Met, low.Response.Breached, low.Response.Pending));
        Assert.Null(low.Response.CompliancePercent);

        Assert.Equal(["high", "mid", "low"], report.Priorities.Select(p => p.Priority));
        Assert.Equal(5, report.Overall.Tickets);
        Assert.Equal((2, 1, 1), (report.Overall.Response.Met, report.Overall.Response.Breached, report.Overall.Response.Pending));
        Assert.Equal(66.7, report.Overall.Response.CompliancePercent);
    }

    [Fact]
    public async Task Averages_AreInMinutes_OverTheTicketsThatHaveAResult()
    {
        var report = await ReportAsync();

        var high = report.Priorities.Single(p => p.Priority == "high");
        Assert.Equal(120.0, high.Response.AverageMinutes); // A 60 min, B 180 min
        Assert.Equal(420.0, high.Resolution.AverageMinutes); // only A is resolved
        Assert.Equal(1800.0, report.Priorities.Single(p => p.Priority == "mid").Resolution.AverageMinutes); // 30 hours
        Assert.Equal(30.0, report.Priorities.Single(p => p.Priority == "low").Response.AverageMinutes);
        Assert.Equal(82.5, report.Overall.Response.AverageMinutes); // (60 + 180 + 60 + 30) / 4, weighted by tickets
    }

    [Fact]
    public async Task TheDateRange_DecidesWhichTicketsCount()
    {
        var narrow = await (await _supervisor.GetAsync($"/api/reports/sla?from=2025-04-01&to=2025-04-01"))
            .Content.ReadFromJsonAsync<SlaReportResponse>();
        var empty = await (await _supervisor.GetAsync("/api/reports/sla?from=2025-04-10&to=2025-04-12"))
            .Content.ReadFromJsonAsync<SlaReportResponse>();

        Assert.Equal(2, narrow!.Overall.Tickets); // A and B
        Assert.Equal(0, empty!.Overall.Tickets);
        Assert.Null(empty.Overall.Response.CompliancePercent);
    }

    [Fact]
    public async Task BreachedTickets_AreListed_NewestFirst_WithBothFlags()
    {
        var response = await _supervisor.GetAsync($"/api/reports/sla/breaches?from={From}&to={To}");
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<BreachedTicketResponse>>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["SLA seed D resolution breached", "SLA seed B response breached"], page.Items.Select(i => i.Subject));
        var d = page.Items[0];
        Assert.False(d.ResponseBreached);
        Assert.True(d.ResolutionBreached);
        Assert.Equal("mid", d.Priority);
        Assert.Matches(@"^TKT-\d{6}$", d.Number);
        Assert.Equal(DateTimeKind.Utc, d.CreatedAt.Kind);
        var b = page.Items[1];
        Assert.True(b.ResponseBreached);
        Assert.True(b.ResolutionBreached); // never resolved and long past its due time
        Assert.Equal(At(1, 13), b.FirstResponseAt);

        // The drill-down target exists: the ticket can be opened.
        Assert.Equal(HttpStatusCode.OK, (await _supervisor.GetAsync($"/api/tickets/{d.TicketId}")).StatusCode);
    }

    [Fact]
    public async Task BreachedTickets_ArePaged()
    {
        var first = (await _supervisor.GetFromJsonAsync<PagedResult<BreachedTicketResponse>>($"/api/reports/sla/breaches?from={From}&to={To}&page=1&pageSize=1"))!;
        var second = (await _supervisor.GetFromJsonAsync<PagedResult<BreachedTicketResponse>>($"/api/reports/sla/breaches?from={From}&to={To}&page=2&pageSize=1"))!;

        Assert.Equal(2, first.TotalCount);
        Assert.Equal("SLA seed D resolution breached", Assert.Single(first.Items).Subject);
        Assert.Equal("SLA seed B response breached", Assert.Single(second.Items).Subject);
        Assert.Equal(1, second.PageSize);
    }

    [Theory]
    [InlineData("/api/reports/sla?from=2025-04-05&to=2025-04-01", "from")]
    [InlineData("/api/reports/sla?from=2020-01-01&to=2025-01-01", "to")]
    [InlineData("/api/reports/sla/breaches?page=0", "page")]
    [InlineData("/api/reports/sla/breaches?pageSize=101", "pageSize")]
    [InlineData("/api/reports/sla/breaches?from=2025-04-05&to=2025-04-01", "from")]
    public async Task InvalidParameters_Return400_WithTheField(string path, string field)
    {
        var response = await _supervisor.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/reports/sla")]
    [InlineData("/api/reports/sla/breaches")]
    public async Task OnlySupervisorAdminAndSuperAdmin_CanOpenIt(string path)
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await _agent.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _supervisor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }
}
