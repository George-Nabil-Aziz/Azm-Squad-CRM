using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Reports;

/// <summary>GET /api/reports/sla: tickets created in the range (<c>yyyy-MM-dd</c>, UTC days, inclusive).</summary>
public sealed record SlaQuery(DateOnly? From, DateOnly? To);

/// <summary>GET /api/reports/sla/breaches: the same range plus paging (page 1.., pageSize 1..100, default 20).</summary>
public sealed record SlaBreachesQuery(DateOnly? From, DateOnly? To, int? Page, int? PageSize);

/// <summary>One SLA target (response or resolution) of a priority: how many tickets met it, breached it, or still wait.</summary>
public sealed record SlaTargetStats(int Met, int Breached, int Pending, double? CompliancePercent, double? AverageMinutes);

public sealed record SlaPriorityRow(string Priority, int Tickets, SlaTargetStats Response, SlaTargetStats Resolution);

public sealed record SlaReportResponse(DateOnly From, DateOnly To, SlaPriorityRow Overall, IReadOnlyList<SlaPriorityRow> Priorities);

public sealed record BreachedTicketResponse(
    Guid TicketId,
    string Number,
    string Subject,
    string Priority,
    string? AssigneeName,
    DateTime CreatedAt,
    DateTime? ResponseDueAt,
    DateTime? FirstResponseAt,
    DateTime? ResolutionDueAt,
    DateTime? ResolvedAt,
    bool ResponseBreached,
    bool ResolutionBreached);

/// <summary>The range (UTC, end exclusive) and the current time that decide "breached" versus "still pending".</summary>
public sealed record SlaFilter(DateTime FromUtc, DateTime ToUtcExclusive, DateTime NowUtc);

/// <summary>What the database counted for one target: met / breached / pending tickets and the sum of the minutes of those with a result.</summary>
public sealed record SlaTargetAggregate(int Met, int Breached, int Pending, double MinutesSum, int Measured);

public sealed record SlaAggregate(TicketPriority Priority, int Tickets, SlaTargetAggregate Response, SlaTargetAggregate Resolution);

public sealed record BreachedTicketRow(
    Guid Id,
    string Prefix,
    int Number,
    string Subject,
    TicketPriority Priority,
    string? AssigneeName,
    DateTime CreatedAt,
    DateTime? ResponseDueAt,
    DateTime? FirstResponseAt,
    DateTime? ResolutionDueAt,
    DateTime? ResolvedAt,
    bool ResponseBreached,
    bool ResolutionBreached);

public interface ISlaReportService
{
    Task<SlaReportResponse> GetAsync(SlaQuery query, CancellationToken cancellationToken);

    Task<PagedResult<BreachedTicketResponse>> ListBreachesAsync(SlaBreachesQuery query, CancellationToken cancellationToken);
}

/// <summary>SLA performance: compliance per priority, averages, and the list of breached tickets.</summary>
public sealed class SlaReportService(IReportsRepository repository, TimeProvider timeProvider) : ISlaReportService
{
    private static readonly TicketPriority[] Priorities = [TicketPriority.High, TicketPriority.Mid, TicketPriority.Low];

    public async Task<SlaReportResponse> GetAsync(SlaQuery query, CancellationToken cancellationToken)
    {
        var (range, filter) = Filter(query.From, query.To);
        var aggregates = await repository.SlaAggregatesAsync(filter, cancellationToken);

        var rows = Priorities.Select(priority =>
        {
            var found = aggregates.FirstOrDefault(a => a.Priority == priority);
            return new SlaAggregate(
                priority,
                found?.Tickets ?? 0,
                found?.Response ?? new SlaTargetAggregate(0, 0, 0, 0, 0),
                found?.Resolution ?? new SlaTargetAggregate(0, 0, 0, 0, 0));
        }).ToList();

        var overall = new SlaAggregate(
            default,
            rows.Sum(r => r.Tickets),
            Sum(rows.Select(r => r.Response)),
            Sum(rows.Select(r => r.Resolution)));

        return new SlaReportResponse(
            range.From, range.To, ToRow("all", overall), [.. rows.Select(r => ToRow(TicketValues.PriorityName(r.Priority), r))]);
    }

    public async Task<PagedResult<BreachedTicketResponse>> ListBreachesAsync(SlaBreachesQuery query, CancellationToken cancellationToken)
    {
        await new SlaBreachesQueryValidator().ValidateOrThrowAsync(query, cancellationToken);
        var (_, filter) = Filter(query.From, query.To);
        var page = query.Page ?? PagingDefaults.DefaultPage;
        var pageSize = query.PageSize ?? PagingDefaults.DefaultPageSize;

        var result = await repository.BreachedTicketsAsync(filter, page, pageSize, cancellationToken);

        return new PagedResult<BreachedTicketResponse>(
            [.. result.Items.Select(r => new BreachedTicketResponse(
                r.Id, Ticket.FormatNumber(r.Number, r.Prefix), r.Subject, TicketValues.PriorityName(r.Priority), r.AssigneeName,
                r.CreatedAt, r.ResponseDueAt, r.FirstResponseAt, r.ResolutionDueAt, r.ResolvedAt, r.ResponseBreached, r.ResolutionBreached))],
            result.Page, result.PageSize, result.TotalCount);
    }

    private (ReportRange Range, SlaFilter Filter) Filter(DateOnly? from, DateOnly? to)
    {
        var range = ReportRangeResolver.Resolve(from, to, timeProvider);
        return (range, new SlaFilter(range.FromUtc, range.ToUtcExclusive, timeProvider.GetUtcNow().UtcDateTime));
    }

    private static SlaTargetAggregate Sum(IEnumerable<SlaTargetAggregate> targets) =>
        targets.Aggregate(new SlaTargetAggregate(0, 0, 0, 0, 0), (a, b) =>
            new SlaTargetAggregate(a.Met + b.Met, a.Breached + b.Breached, a.Pending + b.Pending, a.MinutesSum + b.MinutesSum, a.Measured + b.Measured));

    private static SlaPriorityRow ToRow(string priority, SlaAggregate aggregate) =>
        new(priority, aggregate.Tickets, ToStats(aggregate.Response), ToStats(aggregate.Resolution));

    private static SlaTargetStats ToStats(SlaTargetAggregate target)
    {
        var decided = target.Met + target.Breached;
        return new SlaTargetStats(
            target.Met,
            target.Breached,
            target.Pending,
            decided == 0 ? null : Math.Round(100.0 * target.Met / decided, 1),
            target.Measured == 0 ? null : Math.Round(target.MinutesSum / target.Measured, 1));
    }
}

public sealed class SlaBreachesQueryValidator : AbstractValidator<SlaBreachesQuery>
{
    public SlaBreachesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithName(_ => ReportText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => ReportText.PageSizeField);
    }
}
