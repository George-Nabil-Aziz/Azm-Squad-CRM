using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Branches;

/// <summary>CRM-62 AC 3: every report can be filtered by branch.</summary>
public class BranchReportTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record TicketReportBody(int Total);

    private sealed record SlaBody(SlaRow Overall);

    private sealed record SlaRow(int Tickets);

    private sealed record DashboardBody(int OpenTickets);

    private async Task<(HttpClient SuperAdmin, BranchBody A, BranchBody B)> ArrangeAsync()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var a = await BranchArrange.BranchAsync(superAdmin, "A");
        var b = await BranchArrange.BranchAsync(superAdmin, "B");
        var customerA = await BranchArrange.CustomerAsync(superAdmin, a.Id, "Customer A");
        var customerB = await BranchArrange.CustomerAsync(superAdmin, b.Id, "Customer B");
        await BranchArrange.TicketAsync(superAdmin, customerA.Id);
        await BranchArrange.TicketAsync(superAdmin, customerA.Id);
        await BranchArrange.TicketAsync(superAdmin, customerB.Id);
        return (superAdmin, a, b);
    }

    [Fact]
    public async Task TicketReport_CountsOnlyTheChosenBranch()
    {
        var (superAdmin, a, b) = await ArrangeAsync();

        var forA = await superAdmin.GetFromJsonAsync<TicketReportBody>($"/api/reports/tickets?branchId={a.Id}");
        var forB = await superAdmin.GetFromJsonAsync<TicketReportBody>($"/api/reports/tickets?branchId={b.Id}");
        var all = await superAdmin.GetFromJsonAsync<TicketReportBody>("/api/reports/tickets");

        Assert.Equal(2, forA!.Total);
        Assert.Equal(1, forB!.Total);
        Assert.True(all!.Total >= 3);
    }

    [Fact]
    public async Task SlaReport_DashboardAndTheOtherReports_AcceptTheBranch()
    {
        var (superAdmin, a, _) = await ArrangeAsync();

        var sla = await superAdmin.GetFromJsonAsync<SlaBody>($"/api/reports/sla?branchId={a.Id}");
        var dashboard = await superAdmin.GetFromJsonAsync<DashboardBody>($"/api/reports/dashboard?branchId={a.Id}");

        Assert.Equal(2, sla!.Overall.Tickets);
        Assert.Equal(2, dashboard!.OpenTickets);
        foreach (var path in new[] { "/api/reports/sla/breaches", "/api/reports/agents", "/api/reports/csat", "/api/reports/tickets/export?format=csv&x=1", "/api/reports/agents/export?format=csv&x=1" })
        {
            var url = $"{path}{(path.Contains('?') ? "&" : "?")}branchId={a.Id}";
            Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task ABranchSupervisor_SeesOnlyTheirBranch_EvenWhenAskingForAnother()
    {
        var (superAdmin, a, b) = await ArrangeAsync();
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);

        var own = await supervisor.GetFromJsonAsync<TicketReportBody>("/api/reports/tickets");
        var other = await supervisor.GetFromJsonAsync<TicketReportBody>($"/api/reports/tickets?branchId={b.Id}");

        Assert.Equal(2, own!.Total);
        Assert.Equal(0, other!.Total);
    }
}
