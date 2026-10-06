using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Application.Reports;

/// <summary>The management dashboard (CRM-49): KPI cards and two charts, computed on every request (the client polls).</summary>
public sealed record DashboardResponse(
    DateTime GeneratedAt,
    int OpenTickets,
    int BreachedToday,
    double? AverageResponseMinutes,
    double? AverageCsat,
    int CsatCount,
    IReadOnlyList<DailyCount> TicketsPerDay,
    IReadOnlyList<ReportCount> TicketsByChannel);

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed class DashboardService(IReportsRepository repository, ICsatReadModel csat, TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Days shown by the charts, today included.</summary>
    public const int ChartDays = 14;

    /// <summary>Days behind the average response time and the average CSAT, today included.</summary>
    public const int AverageDays = 30;

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var tomorrowUtc = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var chartFrom = today.AddDays(1 - ChartDays);
        var averageFromUtc = today.AddDays(1 - AverageDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var openTickets = await repository.OpenTicketsAsync(cancellationToken);
        var breachedToday = await repository.BreachedTodayAsync(
            today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), tomorrowUtc, now, cancellationToken);
        var counts = await repository.TicketCountsAsync(
            new TicketReportFilter(chartFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), tomorrowUtc, null, null, null, null),
            cancellationToken);
        var sla = await repository.SlaAggregatesAsync(new SlaFilter(averageFromUtc, tomorrowUtc, now), cancellationToken);
        var ratings = (await csat.GetAsync(new CsatFilter(averageFromUtc, tomorrowUtc), cancellationToken)).Ratings;

        var minutes = sla.Sum(a => a.Response.MinutesSum);
        var measured = sla.Sum(a => a.Response.Measured);

        return new DashboardResponse(
            now,
            openTickets,
            breachedToday,
            measured == 0 ? null : Math.Round(minutes / measured, 1),
            ratings.Count == 0 ? null : Math.Round(ratings.Average(r => r.Rating), 2),
            ratings.Count,
            [.. Enumerable.Range(0, ChartDays).Select(offset => chartFrom.AddDays(offset))
                .Select(day => new DailyCount(day, counts.ByDay.GetValueOrDefault(day)))],
            [.. Enumerable.Range(1, 4).Select(value => (TicketChannel)value)
                .Select(channel => new ReportCount(TicketValues.ChannelName(channel), counts.ByChannel.GetValueOrDefault(channel)))]);
    }
}
