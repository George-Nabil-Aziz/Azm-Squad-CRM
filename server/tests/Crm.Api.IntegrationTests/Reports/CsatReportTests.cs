using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Reports;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Reports;

public class CsatReportTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed class FixedCsat : ICsatReadModel
    {
        public Task<CsatSnapshot> GetAsync(CsatFilter filter, CancellationToken cancellationToken)
        {
            var agent = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var at = new DateTime(2025, 5, 2, 10, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(new CsatSnapshot(
            [
                new CsatRating(Guid.NewGuid(), "TKT-000001", 5, "Great", at, agent, "Sara", null, null),
                new CsatRating(Guid.NewGuid(), "TKT-000002", 1, "Never solved", at.AddHours(1), agent, "Sara", null, null),
            ], 4));
        }
    }

    [Fact]
    public async Task WithoutCrm44_TheReportIsEmpty_NotAnError()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);

        var response = await supervisor.GetAsync("/api/reports/csat?from=2025-05-01&to=2025-05-03");
        var report = (await response.Content.ReadFromJsonAsync<CsatReportResponse>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, report.TotalRatings);
        Assert.Null(report.AverageRating);
        Assert.Equal(3, report.ByDay.Count);
        Assert.Equal(5, report.Distribution.Count);
    }

    [Fact]
    public async Task TheReport_ShowsWhatTheReadModelReturns()
    {
        using var custom = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Scoped<ICsatReadModel, FixedCsat>())));
        var client = custom.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync());

        var report = (await client.GetFromJsonAsync<CsatReportResponse>("/api/reports/csat?from=2025-05-01&to=2025-05-03"))!;

        Assert.Equal(2, report.TotalRatings);
        Assert.Equal(3.0, report.AverageRating);
        Assert.Equal(50.0, report.ResponseRatePercent);
        Assert.Equal("Sara", Assert.Single(report.ByAgent).Name);
        var low = Assert.Single(report.LowRatings);
        Assert.Equal(("TKT-000002", "Never solved"), (low.TicketNumber, low.Comment));
        Assert.Equal(2, report.ByDay.Single(d => d.Date == new DateOnly(2025, 5, 2)).Count);
    }

    [Theory]
    [InlineData("from=2025-05-05&to=2025-05-01", "from")]
    [InlineData("from=2020-01-01&to=2025-05-01", "to")]
    public async Task InvalidRange_Returns400_WithTheField(string query, string field)
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);

        var response = await supervisor.GetAsync($"/api/reports/csat?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OnlySupervisorAdminAndSuperAdmin_CanOpenIt()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/reports/csat")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/reports/csat")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reports/csat")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/reports/csat")).StatusCode);
    }
}
