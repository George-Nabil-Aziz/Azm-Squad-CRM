using Crm.Application.Common.Paging;
using Crm.Application.Reports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Reports;

/// <summary>Report aggregates computed by the database (GROUP BY projections; no tickets are loaded).</summary>
public sealed class ReportsRepository(CrmDbContext db) : IReportsRepository
{
    public async Task<TicketCounts> TicketCountsAsync(TicketReportFilter filter, CancellationToken cancellationToken)
    {
        var tickets = db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= filter.FromUtc && t.CreatedAt < filter.ToUtcExclusive)
            .Where(t => filter.BranchId == null || t.BranchId == filter.BranchId);
        if (filter.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (filter.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        }

        if (filter.Channel is { } channel)
        {
            tickets = tickets.Where(t => t.Channel == channel);
        }

        if (filter.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        var byStatus = await tickets.GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byChannel = await tickets.GroupBy(t => t.Channel)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byPriority = await tickets.GroupBy(t => t.Priority)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byCategory = await tickets.GroupBy(t => t.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Name = db.TicketCategories.Where(c => c.Id == g.Key).Select(c => c.Name).FirstOrDefault(),
                Count = g.Count(),
            })
            .ToListAsync(cancellationToken);
        var byDay = await tickets.GroupBy(t => t.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);

        return new TicketCounts(
            byStatus.ToDictionary(x => x.Key, x => x.Count),
            byChannel.ToDictionary(x => x.Key, x => x.Count),
            byPriority.ToDictionary(x => x.Key, x => x.Count),
            [.. byCategory.Select(x => new CategoryCountRow(x.CategoryId, x.Name, x.Count))],
            byDay.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count));
    }
    public async Task<IReadOnlyList<SlaAggregate>> SlaAggregatesAsync(SlaFilter filter, CancellationToken cancellationToken)
    {
        var now = filter.NowUtc;
        var inRange = db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= filter.FromUtc && t.CreatedAt < filter.ToUtcExclusive)
            .Where(t => filter.BranchId == null || t.BranchId == filter.BranchId);
        var rows = await inRange
            .GroupBy(t => t.Priority)
            .Select(g => new
            {
                Priority = g.Key,
                Tickets = g.Count(),
                ResponseMet = g.Count(t => t.ResponseDueAt != null && t.FirstResponseAt != null && t.FirstResponseAt <= t.ResponseDueAt),
                ResponseBreached = g.Count(t => t.ResponseDueAt != null
                    && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= now))),
                ResponsePending = g.Count(t => t.ResponseDueAt != null && t.FirstResponseAt == null && t.ResponseDueAt > now),
                ResponseMeasured = g.Count(t => t.FirstResponseAt != null),
                ResolutionMet = g.Count(t => t.ResolutionDueAt != null && t.ResolvedAt != null && t.ResolvedAt <= t.ResolutionDueAt),
                ResolutionBreached = g.Count(t => t.ResolutionDueAt != null
                    && ((t.ResolvedAt != null && t.ResolvedAt > t.ResolutionDueAt) || (t.ResolvedAt == null && t.ResolutionDueAt <= now))),
                ResolutionPending = g.Count(t => t.ResolutionDueAt != null && t.ResolvedAt == null && t.ResolutionDueAt > now),
                ResolutionMeasured = g.Count(t => t.ResolvedAt != null),
            })
            .ToListAsync(cancellationToken);

        // Time spans cannot be summed by every database provider (SQLite has no DATEDIFF), so the minutes are summed here
        // from two columns of the tickets that have a result; the counts above stay in SQL.
        var responseMinutes = await MinutesAsync(inRange.Where(t => t.FirstResponseAt != null).Select(t => new Span(t.Priority, t.CreatedAt, t.FirstResponseAt!.Value)), cancellationToken);
        var resolutionMinutes = await MinutesAsync(inRange.Where(t => t.ResolvedAt != null).Select(t => new Span(t.Priority, t.CreatedAt, t.ResolvedAt!.Value)), cancellationToken);

        return
        [
            .. rows.Select(r => new SlaAggregate(
                r.Priority,
                r.Tickets,
                new SlaTargetAggregate(r.ResponseMet, r.ResponseBreached, r.ResponsePending, responseMinutes.GetValueOrDefault(r.Priority), r.ResponseMeasured),
                new SlaTargetAggregate(r.ResolutionMet, r.ResolutionBreached, r.ResolutionPending, resolutionMinutes.GetValueOrDefault(r.Priority), r.ResolutionMeasured))),
        ];
    }

    public Task<int> OpenTicketsAsync(Guid? branchId, CancellationToken cancellationToken) =>
        db.Tickets.AsNoTracking().CountAsync(
            t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && (branchId == null || t.BranchId == branchId),
            cancellationToken);

    public Task<int> BreachedTodayAsync(
        DateTime dayStartUtc, DateTime dayEndUtc, DateTime nowUtc, Guid? branchId, CancellationToken cancellationToken) =>
        db.Tickets.AsNoTracking().Where(t => branchId == null || t.BranchId == branchId).CountAsync(t =>
            (t.ResponseDueAt != null && t.ResponseDueAt >= dayStartUtc && t.ResponseDueAt < dayEndUtc
             && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= nowUtc)))
            || (t.ResolutionDueAt != null && t.ResolutionDueAt >= dayStartUtc && t.ResolutionDueAt < dayEndUtc
                && ((t.ResolvedAt != null && t.ResolvedAt > t.ResolutionDueAt) || (t.ResolvedAt == null && t.ResolutionDueAt <= nowUtc))),
            cancellationToken);

    public async Task<IReadOnlyList<AgentAggregate>> AgentAggregatesAsync(SlaFilter filter, CancellationToken cancellationToken)
    {
        var now = filter.NowUtc;
        var assigned = db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= filter.FromUtc && t.CreatedAt < filter.ToUtcExclusive && t.AssigneeId != null)
            .Where(t => filter.BranchId == null || t.BranchId == filter.BranchId);
        var rows = await assigned
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new
            {
                AgentId = g.Key,
                Name = db.Users.Where(u => u.Id == g.Key).Select(u => u.FullName).FirstOrDefault(),
                Tickets = g.Count(),
                ResponseMet = g.Count(t => t.ResponseDueAt != null && t.FirstResponseAt != null && t.FirstResponseAt <= t.ResponseDueAt),
                ResponseBreached = g.Count(t => t.ResponseDueAt != null
                    && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= now))),
                ResponseMeasured = g.Count(t => t.FirstResponseAt != null),
                ResolutionMet = g.Count(t => t.ResolutionDueAt != null && t.ResolvedAt != null && t.ResolvedAt <= t.ResolutionDueAt),
                ResolutionBreached = g.Count(t => t.ResolutionDueAt != null
                    && ((t.ResolvedAt != null && t.ResolvedAt > t.ResolutionDueAt) || (t.ResolvedAt == null && t.ResolutionDueAt <= now))),
                ResolutionMeasured = g.Count(t => t.ResolvedAt != null),
            })
            .ToListAsync(cancellationToken);

        var responseMinutes = new Dictionary<Guid, double>();
        await foreach (var t in assigned.Where(t => t.FirstResponseAt != null)
                           .Select(t => new { Agent = t.AssigneeId!.Value, t.CreatedAt, End = t.FirstResponseAt!.Value })
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            responseMinutes[t.Agent] = responseMinutes.GetValueOrDefault(t.Agent) + (t.End - t.CreatedAt).TotalMinutes;
        }

        var resolutionMinutes = new Dictionary<Guid, double>();
        await foreach (var t in assigned.Where(t => t.ResolvedAt != null)
                           .Select(t => new { Agent = t.AssigneeId!.Value, t.CreatedAt, End = t.ResolvedAt!.Value })
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            resolutionMinutes[t.Agent] = resolutionMinutes.GetValueOrDefault(t.Agent) + (t.End - t.CreatedAt).TotalMinutes;
        }

        return
        [
            .. rows.Select(r => new AgentAggregate(
                r.AgentId,
                r.Name ?? string.Empty,
                r.Tickets,
                new SlaTargetAggregate(r.ResponseMet, r.ResponseBreached, 0, responseMinutes.GetValueOrDefault(r.AgentId), r.ResponseMeasured),
                new SlaTargetAggregate(r.ResolutionMet, r.ResolutionBreached, 0, resolutionMinutes.GetValueOrDefault(r.AgentId), r.ResolutionMeasured))),
        ];
    }

    private sealed record Span(TicketPriority Priority, DateTime Start, DateTime End);

    private static async Task<Dictionary<TicketPriority, double>> MinutesAsync(IQueryable<Span> spans, CancellationToken cancellationToken)
    {
        var sums = new Dictionary<TicketPriority, double>();
        await foreach (var span in spans.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            sums[span.Priority] = sums.GetValueOrDefault(span.Priority) + (span.End - span.Start).TotalMinutes;
        }

        return sums;
    }

    public async Task<PagedResult<BreachedTicketRow>> BreachedTicketsAsync(
        SlaFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var now = filter.NowUtc;
        var breached = db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= filter.FromUtc && t.CreatedAt < filter.ToUtcExclusive)
            .Where(t => filter.BranchId == null || t.BranchId == filter.BranchId)
            .Where(t =>
                (t.ResponseDueAt != null
                 && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= now)))
                || (t.ResolutionDueAt != null
                    && ((t.ResolvedAt != null && t.ResolvedAt > t.ResolutionDueAt) || (t.ResolvedAt == null && t.ResolutionDueAt <= now))));

        var totalCount = await breached.CountAsync(cancellationToken);
        var items = await breached
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new BreachedTicketRow(
                t.Id,
                t.Prefix,
                t.Number,
                t.Subject,
                t.Priority,
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FullName).FirstOrDefault(),
                t.CreatedAt,
                t.ResponseDueAt,
                t.FirstResponseAt,
                t.ResolutionDueAt,
                t.ResolvedAt,
                t.ResponseDueAt != null
                    && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= now)),
                t.ResolutionDueAt != null
                    && ((t.ResolvedAt != null && t.ResolvedAt > t.ResolutionDueAt) || (t.ResolvedAt == null && t.ResolutionDueAt <= now))))
            .ToListAsync(cancellationToken);

        return new PagedResult<BreachedTicketRow>(items, page, pageSize, totalCount);
    }
}
