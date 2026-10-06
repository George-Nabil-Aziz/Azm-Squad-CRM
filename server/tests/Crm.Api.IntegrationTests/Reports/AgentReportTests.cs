using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Reports;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Reports;

public class AgentReportTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>, IAsyncLifetime
{
    private const string From = "2025-06-01";
    private const string To = "2025-06-05";
    private static readonly DateTime Future = new(2099, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly string SaraEmail = "sara-report@crm.local";
    private static readonly string OmarEmail = "omar-report@crm.local";
    private Guid _sara;
    private Guid _omar;
    private HttpClient _agent = null!;
    private HttpClient _supervisor = null!;

    private static DateTime At(int day, int hour, int minute = 0) => new(2025, 6, day, hour, minute, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        _agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        _supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var existing = await db.Users.Where(u => u.Email == SaraEmail || u.Email == OmarEmail).ToListAsync();
            if (existing.Count == 2)
            {
                _sara = existing.Single(u => u.Email == SaraEmail).Id;
                _omar = existing.Single(u => u.Email == OmarEmail).Id;
                return;
            }
        }

        _sara = await factory.CreateUserAsync(SaraEmail, CrmApiFactory.TestUserPassword, Roles.Agent);
        _omar = await factory.CreateUserAsync(OmarEmail, CrmApiFactory.TestUserPassword, Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(_agent);

        // (agent, created, response due, first response, resolution due, resolved)
        (Guid? Agent, DateTime Created, DateTime? RespDue, DateTime? First, DateTime? ResDue, DateTime? Resolved)[] seeds =
        [
            (_sara, At(1, 9), At(1, 11), At(1, 10), At(1, 17), At(1, 16)),
            (_sara, At(2, 9), At(2, 10), At(2, 12), Future, null),
            (_omar, At(3, 9), null, At(3, 9, 30), null, null),
            (null, At(3, 9), At(3, 10), null, At(3, 17), null), // unassigned: nobody's
            (_sara, At(6, 9), At(6, 10), At(6, 12), Future, null), // after the range
        ];

        foreach (var seed in seeds)
        {
            var created = await TicketArrange.TicketAsync(_agent, customerId, "Agent seed", null, "mid");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await db.Tickets.Where(t => t.Id == created.Id).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AssigneeId, seed.Agent)
                .SetProperty(t => t.CreatedAt, seed.Created)
                .SetProperty(t => t.ResponseDueAt, seed.RespDue)
                .SetProperty(t => t.FirstResponseAt, seed.First)
                .SetProperty(t => t.ResolutionDueAt, seed.ResDue)
                .SetProperty(t => t.ResolvedAt, seed.Resolved));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<AgentReportResponse> ReportAsync()
    {
        var response = await _supervisor.GetAsync($"/api/reports/agents?from={From}&to={To}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AgentReportResponse>())!;
    }

    [Fact]
    public async Task EachAgent_HasTicketsAverageTimesAndSlaPercent()
    {
        var report = await ReportAsync();

        var sara = report.Agents.Single(a => a.AgentId == _sara);
        Assert.Equal(SaraEmail, sara.Name);
        Assert.Equal(2, sara.TicketsHandled);
        Assert.Equal(120.0, sara.AverageFirstResponseMinutes);
        Assert.Equal(420.0, sara.AverageResolutionMinutes);
        Assert.Equal(66.7, sara.SlaPercent);
        Assert.Null(sara.AverageCsat); // CRM-44 is not wired: no ratings

        var omar = report.Agents.Single(a => a.AgentId == _omar);
        Assert.Equal(1, omar.TicketsHandled);
        Assert.Equal(30.0, omar.AverageFirstResponseMinutes);
        Assert.Null(omar.AverageResolutionMinutes);
        Assert.Null(omar.SlaPercent);
    }

    [Fact]
    public async Task OnlyAgentsWithTicketsInTheRangeAreListed_SortedByTickets()
    {
        var report = await ReportAsync();

        var ours = report.Agents.Where(a => a.AgentId == _sara || a.AgentId == _omar).ToList();
        Assert.Equal([_sara, _omar], ours.Select(a => a.AgentId));
        Assert.Equal(3, report.Agents.Sum(a => a.TicketsHandled)); // the unassigned and the out-of-range tickets are not counted
    }

    [Fact]
    public async Task ASupervisor_SeesEveryAgent_NoTeams()
    {
        var report = await ReportAsync();

        Assert.Contains(report.Agents, a => a.AgentId == _sara);
        Assert.Contains(report.Agents, a => a.AgentId == _omar);
    }

    [Fact]
    public async Task Export_Csv_HasOneRowPerAgent()
    {
        var response = await _supervisor.GetAsync($"/api/reports/agents/export?from={From}&to={To}&format=csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("agent-report-2025-06-01_2025-06-05.csv", response.Content.Headers.ContentDisposition!.FileName);
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Agent,Tickets handled", lines[0]);
        Assert.Contains($"{SaraEmail},2,120,420,66.7,", lines);
        Assert.Contains($"{OmarEmail},1,30,,,", lines);
    }

    [Fact]
    public async Task Export_Xlsx_IsAnExcelFile()
    {
        var response = await _supervisor.GetAsync($"/api/reports/agents/export?from={From}&to={To}&format=xlsx");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(ReportExporter.XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = await reader.ReadToEndAsync();
        Assert.Contains($"<t>{SaraEmail}</t>", sheet);
        Assert.Contains("<v>66.7</v>", sheet);
    }

    [Theory]
    [InlineData("/api/reports/agents?from=2025-06-05&to=2025-06-01", "from")]
    [InlineData("/api/reports/agents/export?format=pdf", "format")]
    public async Task InvalidParameters_Return400_WithTheField(string path, string field)
    {
        var response = await _supervisor.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/reports/agents")]
    [InlineData("/api/reports/agents/export?format=csv")]
    public async Task OnlySupervisorAdminAndSuperAdmin_CanOpenIt(string path)
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await _agent.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _supervisor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }
}
