namespace Crm.Application.Reports;

/// <summary>GET /api/reports/agents: tickets created in the range (<c>yyyy-MM-dd</c>, UTC days, inclusive).</summary>
public sealed record AgentQuery(DateOnly? From, DateOnly? To, Guid? BranchId = null);

/// <summary>What the database counted for one agent (the assignee of the tickets).</summary>
public sealed record AgentAggregate(Guid AgentId, string Name, int Tickets, SlaTargetAggregate Response, SlaTargetAggregate Resolution);

public sealed record AgentPerformanceRow(
    Guid AgentId,
    string Name,
    int TicketsHandled,
    double? AverageFirstResponseMinutes,
    double? AverageResolutionMinutes,
    double? SlaPercent,
    double? AverageCsat,
    int CsatCount);

public sealed record AgentReportResponse(DateOnly From, DateOnly To, IReadOnlyList<AgentPerformanceRow> Agents);

public interface IAgentReportService
{
    Task<AgentReportResponse> GetAsync(AgentQuery query, CancellationToken cancellationToken);

    Task<ReportFile> ExportAsync(AgentQuery query, string? format, CancellationToken cancellationToken);
}

/// <summary>
/// Agent performance: tickets handled, average times, SLA % and average CSAT per agent. There is no team concept, so
/// every Supervisor / Admin / SuperAdmin sees all agents (CRM-47 deviation).
/// </summary>
public sealed class AgentReportService(IReportsRepository repository, ICsatReadModel csat, TimeProvider timeProvider) : IAgentReportService
{
    public async Task<AgentReportResponse> GetAsync(AgentQuery query, CancellationToken cancellationToken) =>
        (await BuildAsync(query, cancellationToken)).Report;

    public async Task<ReportFile> ExportAsync(AgentQuery query, string? format, CancellationToken cancellationToken)
    {
        var (range, report) = await BuildAsync(query, cancellationToken);
        var table = new ReportTable(
            "Agents",
            [ReportText.ColumnAgent, ReportText.ColumnTicketsHandled, ReportText.ColumnAvgFirstResponse, ReportText.ColumnAvgResolution,
             ReportText.ColumnSlaPercent, ReportText.ColumnAvgCsat],
            [.. report.Agents.Select(a => (IReadOnlyList<object?>)[
                a.Name, a.TicketsHandled, a.AverageFirstResponseMinutes, a.AverageResolutionMinutes, a.SlaPercent, a.AverageCsat])]);
        return ReportExporter.Export(table, format, $"agent-report-{range.From:yyyy-MM-dd}_{range.To:yyyy-MM-dd}");
    }

    private async Task<(ReportRange Range, AgentReportResponse Report)> BuildAsync(AgentQuery query, CancellationToken cancellationToken)
    {
        var range = ReportRangeResolver.Resolve(query.From, query.To, timeProvider);
        var aggregates = await repository.AgentAggregatesAsync(
            new SlaFilter(range.FromUtc, range.ToUtcExclusive, timeProvider.GetUtcNow().UtcDateTime, query.BranchId), cancellationToken);
        var ratings = (await csat.GetAsync(new CsatFilter(range.FromUtc, range.ToUtcExclusive, query.BranchId), cancellationToken)).Ratings
            .Where(r => r.AgentId is not null)
            .GroupBy(r => r.AgentId!.Value)
            .ToDictionary(g => g.Key, g => (Average: Math.Round(g.Average(r => r.Rating), 2), Count: g.Count()));

        var rows = aggregates
            .OrderByDescending(a => a.Tickets).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(a =>
            {
                var met = a.Response.Met + a.Resolution.Met;
                var decided = met + a.Response.Breached + a.Resolution.Breached;
                ratings.TryGetValue(a.AgentId, out var rating);
                return new AgentPerformanceRow(
                    a.AgentId,
                    a.Name,
                    a.Tickets,
                    Average(a.Response),
                    Average(a.Resolution),
                    decided == 0 ? null : Math.Round(100.0 * met / decided, 1),
                    rating.Count == 0 ? null : rating.Average,
                    rating.Count);
            })
            .ToList();

        return (range, new AgentReportResponse(range.From, range.To, rows));
    }

    private static double? Average(SlaTargetAggregate target) =>
        target.Measured == 0 ? null : Math.Round(target.MinutesSum / target.Measured, 1);
}
