using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Reports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Reports;

public class TicketReportTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>, IAsyncLifetime
{
    // A range of its own, far from "now", so other tests of this class never add tickets to it.
    private static readonly DateTime Day1 = new(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private const string From = "2025-03-01";
    private const string To = "2025-03-05";

    private Guid _categoryId;
    private HttpClient _agent = null!;
    private HttpClient _supervisor = null!;

    public async Task InitializeAsync()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        _agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        _supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        if (await HasSeedAsync())
        {
            _categoryId = await CategoryIdAsync();
            return;
        }

        var customerId = await TicketArrange.CustomerAsync(_agent);
        var category = await TicketArrange.CategoryAsync(superAdmin, "Report");
        _categoryId = category.Id;

        // (created at, status, channel, priority, category)
        (DateTime At, TicketStatus Status, TicketChannel Channel, TicketPriority Priority, bool InCategory)[] seeds =
        [
            (Day1, TicketStatus.New, TicketChannel.Manual, TicketPriority.High, true),
            (Day1.AddHours(1), TicketStatus.Open, TicketChannel.Email, TicketPriority.Mid, true),
            (Day1.AddDays(1), TicketStatus.Open, TicketChannel.Email, TicketPriority.High, false),
            (Day1.AddDays(1).AddHours(2), TicketStatus.Resolved, TicketChannel.WhatsApp, TicketPriority.Low, false),
            (Day1.AddDays(3), TicketStatus.Closed, TicketChannel.Manual, TicketPriority.Mid, true),
            (new DateTime(2025, 3, 5, 23, 59, 59, DateTimeKind.Utc), TicketStatus.Pending, TicketChannel.Portal, TicketPriority.Low, false), // last second of the range
            (new DateTime(2025, 3, 6, 0, 0, 0, DateTimeKind.Utc), TicketStatus.New, TicketChannel.Manual, TicketPriority.High, false), // first second after it
            (new DateTime(2025, 2, 28, 23, 59, 59, DateTimeKind.Utc), TicketStatus.New, TicketChannel.Manual, TicketPriority.High, false), // just before it
        ];

        foreach (var seed in seeds)
        {
            var created = await TicketArrange.TicketAsync(_agent, customerId, "Report seed", seed.InCategory ? category.Id : null, "mid");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == created.Id).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CreatedAt, seed.At)
                .SetProperty(t => t.Status, seed.Status)
                .SetProperty(t => t.Channel, seed.Channel)
                .SetProperty(t => t.Priority, seed.Priority));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> HasSeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets
            .AnyAsync(t => t.CreatedAt >= Day1 && t.CreatedAt < Day1.AddDays(1) && t.Subject == "Report seed");
    }

    private async Task<Guid> CategoryIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        return await db.Tickets.Where(t => t.Subject == "Report seed" && t.CategoryId != null).Select(t => t.CategoryId!.Value).FirstAsync();
    }

    private async Task<List<Ticket>> TicketsInRangeAsync(Func<IQueryable<Ticket>, IQueryable<Ticket>>? narrow = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var from = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 3, 6, 0, 0, 0, DateTimeKind.Utc);
        var query = db.Tickets.AsNoTracking().Where(t => t.CreatedAt >= from && t.CreatedAt < to);
        return await (narrow?.Invoke(query) ?? query).ToListAsync();
    }

    private async Task<TicketReportResponse> GetAsync(string query = "")
    {
        var response = await _supervisor.GetAsync($"/api/reports/tickets?from={From}&to={To}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketReportResponse>())!;
    }

    private static Dictionary<string, int> Counts(IEnumerable<ReportCount> counts) => counts.ToDictionary(c => c.Key, c => c.Count);

    [Fact]
    public async Task Counts_MatchTheDatabase_ForTheDateRange()
    {
        var report = await GetAsync();
        var tickets = await TicketsInRangeAsync();

        Assert.Equal(6, tickets.Count);
        Assert.Equal(tickets.Count, report.Total);
        Assert.Equal(new DateOnly(2025, 3, 1), report.From);
        Assert.Equal(new DateOnly(2025, 3, 5), report.To);
        Assert.Equal(
            tickets.GroupBy(t => TicketValuesName(t.Status)).ToDictionary(g => g.Key, g => g.Count()),
            Counts(report.ByStatus).Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(
            tickets.GroupBy(t => t.Channel.ToString().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count()),
            Counts(report.ByChannel).Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(
            tickets.GroupBy(t => t.Priority.ToString().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count()),
            Counts(report.ByPriority).Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(
            tickets.GroupBy(t => DateOnly.FromDateTime(t.CreatedAt)).ToDictionary(g => g.Key, g => g.Count()),
            report.ByDay.Where(d => d.Count > 0).ToDictionary(d => d.Date, d => d.Count));
        Assert.Equal(tickets.Count(t => t.CategoryId == _categoryId), report.ByCategory.Single(c => c.CategoryId == _categoryId).Count);
        Assert.Equal(tickets.Count(t => t.CategoryId is null), report.ByCategory.Single(c => c.CategoryId is null).Count);
        Assert.Equal(report.Total, report.ByCategory.Sum(c => c.Count));
    }

    [Fact]
    public async Task TheRange_IncludesTheWholeEndDay_AndNothingOutside()
    {
        var report = await GetAsync();

        Assert.Equal(5, report.ByDay.Count);
        Assert.Equal(1, report.ByDay.Single(d => d.Date == new DateOnly(2025, 3, 5)).Count); // 23:59:59 counts
        Assert.Equal(0, report.ByDay.Single(d => d.Date == new DateOnly(2025, 3, 3)).Count); // zero-filled
        Assert.Equal(5, report.ByStatus.Count);
        Assert.Equal(4, report.ByChannel.Count);
        Assert.Equal(3, report.ByPriority.Count);
    }

    [Theory]
    [InlineData("status=open")]
    [InlineData("channel=email")]
    [InlineData("priority=high")]
    [InlineData("status=open&channel=email")]
    [InlineData("channel=manual&priority=mid")]
    public async Task Filters_NarrowEveryBreakdown(string filter)
    {
        var values = filter.Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        var tickets = await TicketsInRangeAsync(q => q.Where(t =>
            (!values.ContainsKey("status") || t.Status.ToString().ToLower() == values["status"])
            && (!values.ContainsKey("channel") || t.Channel.ToString().ToLower() == values["channel"])
            && (!values.ContainsKey("priority") || t.Priority.ToString().ToLower() == values["priority"])));

        var report = await GetAsync("&" + filter);

        Assert.True(tickets.Count > 0);
        Assert.Equal(tickets.Count, report.Total);
        Assert.Equal(tickets.Count, report.ByChannel.Sum(c => c.Count));
        Assert.Equal(tickets.Count, report.ByPriority.Sum(c => c.Count));
        Assert.Equal(tickets.Count, report.ByDay.Sum(c => c.Count));
    }

    [Fact]
    public async Task TheCategoryFilter_KeepsOnlyThatCategory()
    {
        var tickets = await TicketsInRangeAsync(q => q.Where(t => t.CategoryId == _categoryId));

        var report = await GetAsync($"&categoryId={_categoryId}");

        Assert.Equal(tickets.Count, report.Total);
        var only = Assert.Single(report.ByCategory);
        Assert.Equal(_categoryId, only.CategoryId);
        Assert.StartsWith("Report ", only.Name);
    }

    [Fact]
    public async Task AFilterThatMatchesNothing_ReturnsZeros()
    {
        var report = await GetAsync($"&categoryId={Guid.NewGuid()}");

        Assert.Equal(0, report.Total);
        Assert.Empty(report.ByCategory);
        Assert.All(report.ByDay, d => Assert.Equal(0, d.Count));
    }

    [Fact]
    public async Task WithoutDates_TheLast30DaysAreReported()
    {
        var report = (await (await _supervisor.GetAsync("/api/reports/tickets")).Content.ReadFromJsonAsync<TicketReportResponse>())!;

        Assert.Equal(30, report.ByDay.Count);
        Assert.Equal(DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime), report.To);
    }

    [Theory]
    [InlineData("from=2025-03-05&to=2025-03-01", "from")]
    [InlineData("from=2020-01-01&to=2025-01-01", "to")]
    [InlineData("status=archived", "status")]
    [InlineData("channel=fax", "channel")]
    [InlineData("priority=urgent", "priority")]
    public async Task InvalidParameters_Return400_WithTheField(string query, string field)
    {
        var response = await _supervisor.GetAsync($"/api/reports/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Export_Csv_HasTheReportRows()
    {
        var response = await _supervisor.GetAsync($"/api/reports/tickets/export?from={From}&to={To}&format=csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("ticket-report-2025-03-01_2025-03-05.csv", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Report,Value,Count", lines[0]);
        Assert.Contains("Status,open,2", lines);
        Assert.Contains("Channel,email,2", lines);
        Assert.Contains("Day,2025-03-05,1", lines);
        Assert.Contains("Day,2025-03-03,0", lines);
    }

    [Fact]
    public async Task Export_Xlsx_IsAnExcelFile_WithTheSameNumbers()
    {
        var response = await _supervisor.GetAsync($"/api/reports/tickets/export?from={From}&to={To}&format=xlsx&status=open");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReportExporter.XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition!.FileName);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = await reader.ReadToEndAsync();
        Assert.Contains("<t>Report</t>", sheet);
        Assert.Contains("<t>open</t>", sheet);
        Assert.Contains("<v>2</v>", sheet);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("")]
    public async Task Export_WithAnUnknownFormat_Returns400(string format)
    {
        var response = await _supervisor.GetAsync($"/api/reports/tickets/export?from={From}&to={To}&format={format}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"format\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/reports/tickets")]
    [InlineData("/api/reports/tickets/export?format=csv")]
    public async Task OnlySupervisorAdminAndSuperAdmin_CanOpenReports(string path)
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await _agent.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _supervisor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }

    private static string TicketValuesName(TicketStatus status) => status.ToString().ToLowerInvariant();
}
