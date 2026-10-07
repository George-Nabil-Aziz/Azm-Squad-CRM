using Crm.Application.Common.Paging;
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

    public IReadOnlyList<SlaAggregate> SlaAggregates { get; set; } = [];

    public SlaFilter? LastSlaFilter { get; private set; }

    public PagedResult<BreachedTicketRow> Breaches { get; set; } = new([], 1, 20, 0);

    public (int Page, int PageSize)? LastBreachPaging { get; private set; }

    public IReadOnlyList<AgentAggregate> AgentAggregates { get; set; } = [];

    public Task<IReadOnlyList<AgentAggregate>> AgentAggregatesAsync(SlaFilter filter, CancellationToken cancellationToken)
    {
        LastSlaFilter = filter;
        return Task.FromResult(AgentAggregates);
    }

    public int OpenTickets { get; set; }

    public int BreachedToday { get; set; }

    public (DateTime Start, DateTime End, DateTime Now)? LastToday { get; private set; }

    public Guid? LastBranchId { get; private set; }

    public Task<int> OpenTicketsAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        LastBranchId = branchId;
        return Task.FromResult(OpenTickets);
    }

    public Task<int> BreachedTodayAsync(
        DateTime dayStartUtc, DateTime dayEndUtc, DateTime nowUtc, Guid? branchId, CancellationToken cancellationToken)
    {
        LastToday = (dayStartUtc, dayEndUtc, nowUtc);
        return Task.FromResult(BreachedToday);
    }

    public List<TicketReportFilter> TicketFilters { get; } = [];

    public Task<TicketCounts> TicketCountsAsync(TicketReportFilter filter, CancellationToken cancellationToken)
    {
        LastTicketFilter = filter;
        TicketFilters.Add(filter);
        return Task.FromResult(TicketCounts);
    }

    public Task<IReadOnlyList<SlaAggregate>> SlaAggregatesAsync(SlaFilter filter, CancellationToken cancellationToken)
    {
        LastSlaFilter = filter;
        return Task.FromResult(SlaAggregates);
    }

    public Task<PagedResult<BreachedTicketRow>> BreachedTicketsAsync(
        SlaFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastSlaFilter = filter;
        LastBreachPaging = (page, pageSize);
        return Task.FromResult(Breaches);
    }
}

internal sealed class ReportClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
