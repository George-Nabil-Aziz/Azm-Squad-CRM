using Crm.Application.Common.Paging;
using Crm.Domain.Tickets;

namespace Crm.Application.Reports;

/// <summary>GET /api/reports/tickets query string. Dates are <c>yyyy-MM-dd</c> (UTC days, inclusive); names as in the ticket API.</summary>
public sealed record TicketReportQuery(
    DateOnly? From, DateOnly? To, string? Status, Guid? CategoryId, string? Channel, string? Priority);

/// <summary>A bucket of a breakdown: the API name ("open", "email", "high") and the number of tickets.</summary>
public sealed record ReportCount(string Key, int Count);

public sealed record CategoryCount(Guid? CategoryId, string? Name, int Count);

public sealed record DailyCount(DateOnly Date, int Count);

public sealed record TicketReportResponse(
    DateOnly From,
    DateOnly To,
    int Total,
    IReadOnlyList<ReportCount> ByStatus,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<ReportCount> ByChannel,
    IReadOnlyList<ReportCount> ByPriority,
    IReadOnlyList<DailyCount> ByDay);

/// <summary>The filter handed to the repository: UTC range (end exclusive) plus the optional ticket filters.</summary>
public sealed record TicketReportFilter(
    DateTime FromUtc, DateTime ToUtcExclusive, TicketStatus? Status, Guid? CategoryId, TicketChannel? Channel, TicketPriority? Priority);

public sealed record CategoryCountRow(Guid? CategoryId, string? Name, int Count);

/// <summary>Ticket counts per breakdown, as the database grouped them (only buckets that have tickets).</summary>
public sealed record TicketCounts(
    IReadOnlyDictionary<TicketStatus, int> ByStatus,
    IReadOnlyDictionary<TicketChannel, int> ByChannel,
    IReadOnlyDictionary<TicketPriority, int> ByPriority,
    IReadOnlyList<CategoryCountRow> ByCategory,
    IReadOnlyDictionary<DateOnly, int> ByDay);

/// <summary>Report storage (implemented in Crm.Infrastructure with EF Core aggregate queries; nothing is loaded into memory).</summary>
public interface IReportsRepository
{
    Task<TicketCounts> TicketCountsAsync(TicketReportFilter filter, CancellationToken cancellationToken);

    /// <summary>Per priority (only priorities that have tickets): SLA counts and minute sums of the tickets created in the range.</summary>
    Task<IReadOnlyList<SlaAggregate>> SlaAggregatesAsync(SlaFilter filter, CancellationToken cancellationToken);

    /// <summary>Tickets of the range whose response or resolution is breached at <c>NowUtc</c>, newest first.</summary>
    Task<PagedResult<BreachedTicketRow>> BreachedTicketsAsync(
        SlaFilter filter, int page, int pageSize, CancellationToken cancellationToken);
}

public interface ITicketReportService
{
    Task<TicketReportResponse> GetAsync(TicketReportQuery query, CancellationToken cancellationToken);

    Task<ReportFile> ExportAsync(TicketReportQuery query, string? format, CancellationToken cancellationToken);
}
