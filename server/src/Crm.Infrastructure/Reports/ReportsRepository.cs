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
            .Where(t => t.CreatedAt >= filter.FromUtc && t.CreatedAt < filter.ToUtcExclusive);
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
}
