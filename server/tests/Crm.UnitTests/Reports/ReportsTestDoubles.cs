using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Reports;

/// <summary>Reports storage a test fills by hand; remembers the filters it was asked for.</summary>
internal sealed class FakeReportsRepository : IReportsRepository
{
    public TicketCounts TicketCounts { get; set; } = new(
        new Dictionary<TicketStatus, int>(), new Dictionary<TicketChannel, int>(), new Dictionary<TicketPriority, int>(), [],
        new Dictionary<DateOnly, int>());

    public TicketReportFilter? LastTicketFilter { get; private set; }

    public Task<TicketCounts> TicketCountsAsync(TicketReportFilter filter, CancellationToken cancellationToken)
    {
        LastTicketFilter = filter;
        return Task.FromResult(TicketCounts);
    }
}

internal sealed class ReportClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
