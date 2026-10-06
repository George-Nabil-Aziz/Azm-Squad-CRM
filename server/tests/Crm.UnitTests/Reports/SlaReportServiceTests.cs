using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Reports;

public class SlaReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeReportsRepository _repository = new();
    private readonly SlaReportService _service;

    public SlaReportServiceTests()
    {
        _service = new SlaReportService(_repository, new ReportClock(Now));
    }

    private static SlaTargetAggregate Target(int met, int breached, int pending, double minutesSum, int measured) =>
        new(met, breached, pending, minutesSum, measured);

    private static SlaQuery Range(string from = "2026-10-01", string to = "2026-10-05") =>
        new(DateOnly.Parse(from), DateOnly.Parse(to));

    [Fact]
    public async Task EveryPriorityIsListed_HighMidLow_WithZerosWhenThereAreNoTickets()
    {
        var report = await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(["high", "mid", "low"], report.Priorities.Select(p => p.Priority));
        Assert.All(report.Priorities, p =>
        {
            Assert.Equal(0, p.Tickets);
            Assert.Null(p.Response.CompliancePercent);
            Assert.Null(p.Response.AverageMinutes);
            Assert.Null(p.Resolution.CompliancePercent);
        });
        Assert.Equal("all", report.Overall.Priority);
        Assert.Equal(0, report.Overall.Tickets);
    }

    [Fact]
    public async Task Compliance_IsMetOverMetPlusBreached_PendingIsLeftOut()
    {
        _repository.SlaAggregates =
        [
            new SlaAggregate(TicketPriority.High, 10, Target(6, 2, 2, 0, 0), Target(1, 1, 8, 0, 0)),
            new SlaAggregate(TicketPriority.Low, 3, Target(0, 0, 3, 0, 0), Target(0, 0, 3, 0, 0)),
        ];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        var high = report.Priorities.Single(p => p.Priority == "high");
        Assert.Equal((6, 2, 2), (high.Response.Met, high.Response.Breached, high.Response.Pending));
        Assert.Equal(75.0, high.Response.CompliancePercent);
        Assert.Equal(50.0, high.Resolution.CompliancePercent);
        var low = report.Priorities.Single(p => p.Priority == "low");
        Assert.Null(low.Response.CompliancePercent); // nothing decided yet
        Assert.Equal(3, low.Response.Pending);
    }

    [Fact]
    public async Task Compliance_IsRoundedToOneDecimal()
    {
        _repository.SlaAggregates = [new SlaAggregate(TicketPriority.Mid, 3, Target(1, 2, 0, 0, 0), Target(0, 0, 0, 0, 0))];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(33.3, report.Priorities.Single(p => p.Priority == "mid").Response.CompliancePercent);
    }

    [Fact]
    public async Task Averages_AreSumOverMeasured_AndTheOverallRowIsWeighted()
    {
        _repository.SlaAggregates =
        [
            new SlaAggregate(TicketPriority.High, 2, Target(2, 0, 0, 60, 2), Target(1, 0, 1, 240, 1)),
            new SlaAggregate(TicketPriority.Mid, 3, Target(1, 1, 1, 330, 2), Target(0, 0, 3, 0, 0)),
        ];

        var report = await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(30.0, report.Priorities.Single(p => p.Priority == "high").Response.AverageMinutes);
        Assert.Equal(165.0, report.Priorities.Single(p => p.Priority == "mid").Response.AverageMinutes);
        Assert.Equal(240.0, report.Priorities.Single(p => p.Priority == "high").Resolution.AverageMinutes);
        Assert.Null(report.Priorities.Single(p => p.Priority == "mid").Resolution.AverageMinutes);
        Assert.Equal(5, report.Overall.Tickets);
        Assert.Equal(97.5, report.Overall.Response.AverageMinutes); // (60 + 330) / 4, not (30 + 165) / 2
        Assert.Equal((3, 1, 1), (report.Overall.Response.Met, report.Overall.Response.Breached, report.Overall.Response.Pending));
        Assert.Equal(75.0, report.Overall.Response.CompliancePercent);
        Assert.Equal(240.0, report.Overall.Resolution.AverageMinutes);
    }

    [Fact]
    public async Task TheRepository_GetsTheUtcRange_AndTheCurrentTime()
    {
        await _service.GetAsync(Range(), CancellationToken.None);

        Assert.Equal(
            new SlaFilter(
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc), Now.UtcDateTime),
            _repository.LastSlaFilter);
    }

    [Fact]
    public async Task WithoutDates_TheLast30DaysAreUsed()
    {
        var report = await _service.GetAsync(new SlaQuery(null, null), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 6), report.To);
        Assert.Equal(new DateOnly(2026, 9, 7), report.From);
    }

    [Fact]
    public async Task BadRange_IsAValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetAsync(Range("2026-10-05", "2026-10-01"), CancellationToken.None));
    }

    [Fact]
    public async Task Breaches_MapTheRows_WithTheTicketNumber_AndDefaultPaging()
    {
        var id = Guid.NewGuid();
        var created = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
        _repository.Breaches = new PagedResult<BreachedTicketRow>(
            [new BreachedTicketRow(id, "SUP-", 12, "Printer", TicketPriority.High, "Sara", created, created.AddHours(2), null,
                created.AddHours(8), null, true, true)], 1, 20, 1);

        var page = await _service.ListBreachesAsync(new SlaBreachesQuery(DateOnly.Parse("2026-10-01"), DateOnly.Parse("2026-10-05"), null, null),
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal((id, "SUP-000012", "high", "Sara"), (item.TicketId, item.Number, item.Priority, item.AssigneeName));
        Assert.True(item.ResponseBreached && item.ResolutionBreached);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal((1, 20), _repository.LastBreachPaging);
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    public async Task BreachPaging_OutOfRange_IsAValidationError(int page, int pageSize, string field)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListBreachesAsync(new SlaBreachesQuery(null, null, page, pageSize), CancellationToken.None));

        Assert.Contains(field, error.Errors.Keys);
    }
}
