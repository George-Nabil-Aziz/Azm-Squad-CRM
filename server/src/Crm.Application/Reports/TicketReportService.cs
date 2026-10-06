using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Application.Reports;

/// <summary>Ticket volume report: validation, the UTC range, zero-filled breakdowns, export.</summary>
public sealed class TicketReportService(IReportsRepository repository, TimeProvider timeProvider) : ITicketReportService
{
    public async Task<TicketReportResponse> GetAsync(TicketReportQuery query, CancellationToken cancellationToken) =>
        (await BuildAsync(query, cancellationToken)).Response;

    public async Task<ReportFile> ExportAsync(TicketReportQuery query, string? format, CancellationToken cancellationToken)
    {
        var (range, report) = await BuildAsync(query, cancellationToken);

        List<IReadOnlyList<object?>> rows = [];
        rows.AddRange(report.ByStatus.Select(c => Row(ReportText.SectionStatus, c.Key, c.Count)));
        rows.AddRange(report.ByCategory.Select(c => Row(ReportText.SectionCategory, c.Name ?? ReportText.Uncategorized, c.Count)));
        rows.AddRange(report.ByChannel.Select(c => Row(ReportText.SectionChannel, c.Key, c.Count)));
        rows.AddRange(report.ByPriority.Select(c => Row(ReportText.SectionPriority, c.Key, c.Count)));
        rows.AddRange(report.ByDay.Select(d => Row(ReportText.SectionDay, d.Date, d.Count)));

        var table = new ReportTable("Tickets", [ReportText.ColumnReport, ReportText.ColumnValue, ReportText.ColumnCount], rows);
        return ReportExporter.Export(table, format, $"ticket-report-{range.From:yyyy-MM-dd}_{range.To:yyyy-MM-dd}");
    }

    private static IReadOnlyList<object?> Row(string section, object value, int count) => [section, value, count];

    private async Task<(ReportRange Range, TicketReportResponse Response)> BuildAsync(
        TicketReportQuery query, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        TicketStatus? status = null;
        TicketChannel? channel = null;
        TicketPriority? priority = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (TicketValues.TryParseStatus(query.Status, out var parsed)) status = parsed;
            else errors["status"] = [ReportText.StatusInvalid];
        }

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            if (TryParseChannel(query.Channel, out var parsed)) channel = parsed;
            else errors["channel"] = [ReportText.ChannelInvalid];
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            if (TicketValues.TryParsePriority(query.Priority, out var parsed)) priority = parsed;
            else errors["priority"] = [ReportText.PriorityInvalid];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var range = ReportRangeResolver.Resolve(query.From, query.To, timeProvider);
        var counts = await repository.TicketCountsAsync(
            new TicketReportFilter(range.FromUtc, range.ToUtcExclusive, status, query.CategoryId, channel, priority), cancellationToken);

        var byStatus = Enum.GetValues<TicketStatus>().Select(s => new ReportCount(TicketValues.StatusName(s), counts.ByStatus.GetValueOrDefault(s))).ToList();
        var byChannel = Enum.GetValues<TicketChannel>().Select(c => new ReportCount(TicketValues.ChannelName(c), counts.ByChannel.GetValueOrDefault(c))).ToList();
        var byPriority = new[] { TicketPriority.High, TicketPriority.Mid, TicketPriority.Low }
            .Select(p => new ReportCount(TicketValues.PriorityName(p), counts.ByPriority.GetValueOrDefault(p))).ToList();
        var byCategory = counts.ByCategory
            .OrderByDescending(c => c.Count).ThenBy(c => c.CategoryId is null).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new CategoryCount(c.CategoryId, c.Name, c.Count))
            .ToList();
        var byDay = Enumerable.Range(0, range.Days)
            .Select(offset => range.From.AddDays(offset))
            .Select(day => new DailyCount(day, counts.ByDay.GetValueOrDefault(day)))
            .ToList();

        var response = new TicketReportResponse(range.From, range.To, byStatus.Sum(c => c.Count), byStatus, byCategory, byChannel, byPriority, byDay);
        return (range, response);
    }

    /// <summary>"email", "WhatsApp", … (any case; numbers are not accepted).</summary>
    private static bool TryParseChannel(string name, out TicketChannel channel) =>
        Enum.TryParse(name.Trim(), ignoreCase: true, out channel)
        && Enum.IsDefined(channel)
        && !char.IsDigit(name.Trim()[0]);
}
