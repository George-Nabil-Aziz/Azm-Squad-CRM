using Crm.Application.Reports;

namespace Crm.UnitTests.Reports;

/// <summary>CRM-62 AC 3: every report passes the optional branch to its storage.</summary>
public class ReportBranchFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 15, 30, 0, TimeSpan.Zero);
    private static readonly Guid Branch = Guid.NewGuid();
    private readonly FakeReportsRepository _repository = new();
    private readonly FakeCsatReadModel _csat = new();
    private readonly ReportClock _clock = new(Now);

    [Fact]
    public async Task TicketReport_PassesTheBranch()
    {
        await new TicketReportService(_repository, _clock).GetAsync(
            new TicketReportQuery(null, null, null, null, null, null, Branch), CancellationToken.None);

        Assert.Equal(Branch, _repository.LastTicketFilter!.BranchId);
    }

    [Fact]
    public async Task SlaReport_AndBreaches_PassTheBranch()
    {
        var service = new SlaReportService(_repository, _clock);

        await service.GetAsync(new SlaQuery(null, null, Branch), CancellationToken.None);
        Assert.Equal(Branch, _repository.LastSlaFilter!.BranchId);

        await service.ListBreachesAsync(new SlaBreachesQuery(null, null, null, null, Branch), CancellationToken.None);
        Assert.Equal(Branch, _repository.LastSlaFilter!.BranchId);
    }

    [Fact]
    public async Task AgentReport_PassesTheBranch()
    {
        await new AgentReportService(_repository, _csat, _clock).GetAsync(new AgentQuery(null, null, Branch), CancellationToken.None);

        Assert.Equal(Branch, _repository.LastSlaFilter!.BranchId);
        Assert.Equal(Branch, _csat.LastFilter!.BranchId);
    }

    [Fact]
    public async Task CsatReport_PassesTheBranch()
    {
        await new CsatReportService(_csat, _clock).GetAsync(new CsatQuery(null, null, Branch), CancellationToken.None);

        Assert.Equal(Branch, _csat.LastFilter!.BranchId);
    }

    [Fact]
    public async Task Dashboard_PassesTheBranchToEveryQuery()
    {
        await new DashboardService(_repository, _csat, _clock).GetAsync(Branch, CancellationToken.None);

        Assert.Equal(Branch, _repository.LastBranchId);
        Assert.Equal(Branch, _repository.LastTicketFilter!.BranchId);
        Assert.Equal(Branch, _repository.LastSlaFilter!.BranchId);
        Assert.Equal(Branch, _csat.LastFilter!.BranchId);
    }

    [Fact]
    public async Task WithoutABranch_NothingIsFiltered()
    {
        await new TicketReportService(_repository, _clock).GetAsync(new TicketReportQuery(null, null, null, null, null, null), CancellationToken.None);

        Assert.Null(_repository.LastTicketFilter!.BranchId);
    }
}
