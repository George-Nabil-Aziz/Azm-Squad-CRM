using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Reports;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 15, 30, 0, TimeSpan.Zero);
    private readonly FakeReportsRepository _repository = new();
    private readonly FakeCsatReadModel _csat = new();
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _service = new DashboardService(_repository, _csat, new ReportClock(Now));
    }

    private static SlaTargetAggregate Target(double minutes, int measured) => new(0, 0, 0, minutes, measured);

    [Fact]
    public async Task Kpis_ComeFromTheRepositoryAndTheReadModel()
    {
        _repository.OpenTickets = 12;
        _repository.BreachedToday = 3;
        _repository.SlaAggregates =
        [
            new SlaAggregate(TicketPriority.High, 4, Target(120, 2), Target(0, 0)),
            new SlaAggregate(TicketPriority.Low, 2, Target(180, 1), Target(0, 0)),
        ];
        _csat.Snapshot = new CsatSnapshot(
        [
            new CsatRating(Guid.NewGuid(), "T", 5, null, Now.UtcDateTime, null, null, null, null),
            new CsatRating(Guid.NewGuid(), "T", 4, null, Now.UtcDateTime, null, null, null, null),
        ], 0);

        var dashboard = await _service.GetAsync(null, CancellationToken.None);

        Assert.Equal(12, dashboard.OpenTickets);
        Assert.Equal(3, dashboard.BreachedToday);
        Assert.Equal(100.0, dashboard.AverageResponseMinutes); // (120 + 180) / 3
        Assert.Equal((4.5, 2), (dashboard.AverageCsat, dashboard.CsatCount));
        Assert.Equal(Now.UtcDateTime, dashboard.GeneratedAt);
    }

    [Fact]
    public async Task WithoutData_AveragesAreNull()
    {
        var dashboard = await _service.GetAsync(null, CancellationToken.None);

        Assert.Equal(0, dashboard.OpenTickets);
        Assert.Null(dashboard.AverageResponseMinutes);
        Assert.Null(dashboard.AverageCsat);
        Assert.Equal(0, dashboard.CsatCount);
    }

    [Fact]
    public async Task Charts_ListEveryDayOfTheLast14Days_AndEveryChannel()
    {
        _repository.TicketCounts = _repository.TicketCounts with
        {
            ByDay = new Dictionary<DateOnly, int> { [new DateOnly(2026, 10, 6)] = 5, [new DateOnly(2026, 9, 25)] = 2 },
            ByChannel = new Dictionary<TicketChannel, int> { [TicketChannel.Email] = 6, [TicketChannel.Manual] = 1 },
        };

        var dashboard = await _service.GetAsync(null, CancellationToken.None);

        Assert.Equal(14, dashboard.TicketsPerDay.Count);
        Assert.Equal(new DateOnly(2026, 9, 23), dashboard.TicketsPerDay[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 6), dashboard.TicketsPerDay[^1].Date);
        Assert.Equal(5, dashboard.TicketsPerDay[^1].Count);
        Assert.Equal(2, dashboard.TicketsPerDay.Single(d => d.Date == new DateOnly(2026, 9, 25)).Count);
        Assert.Equal(["manual", "email", "whatsapp", "portal", "webform", "chat", "sms"], dashboard.TicketsByChannel.Select(c => c.Key));
        Assert.Equal([1, 6, 0, 0, 0, 0, 0], dashboard.TicketsByChannel.Select(c => c.Count));
    }

    [Fact]
    public async Task TheWindows_AreTodayAnd14And30Days()
    {
        await _service.GetAsync(null, CancellationToken.None);

        Assert.Equal(
            (new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), Now.UtcDateTime),
            _repository.LastToday);
        Assert.Contains(_repository.TicketFilters, f => f.FromUtc == new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc)
                                                         && f.ToUtcExclusive == new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), _repository.LastSlaFilter!.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), _csat.LastFilter!.FromUtc);
    }
}
