using Crm.Application.Common.Exceptions;
using Crm.Application.Reports;

namespace Crm.UnitTests.Reports;

public class AgentReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Sara = Guid.NewGuid();
    private static readonly Guid Omar = Guid.NewGuid();
    private readonly FakeReportsRepository _repository = new();
    private readonly FakeCsatReadModel _csat = new();
    private readonly AgentReportService _service;

    public AgentReportServiceTests()
    {
        _service = new AgentReportService(_repository, _csat, new ReportClock(Now));
    }

    private static SlaTargetAggregate Target(int met, int breached, double minutes, int measured) => new(met, breached, 0, minutes, measured);

    private static AgentQuery Range(string from = "2026-10-01", string to = "2026-10-03") => new(DateOnly.Parse(from), DateOnly.Parse(to));

    private static CsatRating Rating(Guid? agent, int stars) =>
        new(Guid.NewGuid(), "TKT-000001", stars, null, new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), agent, "x", null, null);

    [Fact]
    public async Task EachAgent_GetsTicketsAveragesAndASlaPercent_OverBothTargets()
    {
        _repository.AgentAggregates =
        [
            new AgentAggregate(Sara, "Sara", 6, Target(3, 1, 120, 4), Target(2, 2, 3000, 4)), // (3 + 2) / 8 = 62.5 %
            new AgentAggregate(Omar, "Omar", 2, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
        ];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        var sara = report.Agents.Single(a => a.AgentId == Sara);
        Assert.Equal(6, sara.TicketsHandled);
        Assert.Equal(30.0, sara.AverageFirstResponseMinutes);
        Assert.Equal(750.0, sara.AverageResolutionMinutes);
        Assert.Equal(62.5, sara.SlaPercent);
        var omar = report.Agents.Single(a => a.AgentId == Omar);
        Assert.Null(omar.AverageFirstResponseMinutes);
        Assert.Null(omar.AverageResolutionMinutes);
        Assert.Null(omar.SlaPercent);
    }

    [Fact]
    public async Task AgentsAreSortedByTicketsThenName()
    {
        _repository.AgentAggregates =
        [
            new AgentAggregate(Omar, "Omar", 2, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
            new AgentAggregate(Sara, "Sara", 2, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
            new AgentAggregate(Guid.NewGuid(), "Zed", 5, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
        ];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(["Zed", "Omar", "Sara"], report.Agents.Select(a => a.Name));
    }

    [Fact]
    public async Task AverageCsat_ComesFromTheReadModel_PerAgent()
    {
        _repository.AgentAggregates =
        [
            new AgentAggregate(Sara, "Sara", 1, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
            new AgentAggregate(Omar, "Omar", 1, Target(0, 0, 0, 0), Target(0, 0, 0, 0)),
        ];
        _csat.Snapshot = new CsatSnapshot([Rating(Sara, 5), Rating(Sara, 4), Rating(Guid.NewGuid(), 1), Rating(null, 1)], 0);

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        var sara = report.Agents.Single(a => a.AgentId == Sara);
        Assert.Equal((4.5, 2), (sara.AverageCsat, sara.CsatCount));
        Assert.Null(report.Agents.Single(a => a.AgentId == Omar).AverageCsat);
    }

    [Fact]
    public async Task WithoutCsatRatings_TheAverageIsNull()
    {
        _repository.AgentAggregates = [new AgentAggregate(Sara, "Sara", 1, Target(0, 0, 0, 0), Target(0, 0, 0, 0))];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Null(report.Agents.Single().AverageCsat);
        Assert.Equal(0, report.Agents.Single().CsatCount);
    }

    [Fact]
    public async Task TheRepository_GetsTheUtcRange_AndNow()
    {
        await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(
            new SlaFilter(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), Now.UtcDateTime),
            _repository.LastSlaFilter);
        Assert.Equal(new CsatFilter(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)), _csat.LastFilter);
    }

    [Fact]
    public async Task BadRange_IsAValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetAsync(Range("2026-10-05", "2026-10-01"), CancellationToken.None));
    }

    [Fact]
    public async Task Export_WritesOneRowPerAgent_AsCsv()
    {
        _repository.AgentAggregates = [new AgentAggregate(Sara, "Sara, Senior", 6, Target(3, 1, 120, 4), Target(2, 2, 3000, 4))];

        var file = await _service.ExportAsync(Range(), "csv", CancellationToken.None);

        var lines = System.Text.Encoding.UTF8.GetString(file.Content).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Agent,Tickets handled,Average first response (min),Average resolution (min),SLA %,Average CSAT", lines[0]);
        Assert.Equal("\"Sara, Senior\",6,30,750,62.5,", lines[1]);
        Assert.Equal("agent-report-2026-10-01_2026-10-03.csv", file.FileName);
    }

    [Fact]
    public async Task Export_WithAnUnknownFormat_IsAValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ExportAsync(Range(), "pdf", CancellationToken.None));
    }
}
