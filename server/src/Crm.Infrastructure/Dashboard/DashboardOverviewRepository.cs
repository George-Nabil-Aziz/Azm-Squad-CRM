using Crm.Application.Dashboard;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Dashboard;

/// <summary>Counts of the staff home dashboard, computed by the database.</summary>
public sealed class DashboardOverviewRepository(CrmDbContext db) : IDashboardOverviewRepository
{
    private IQueryable<Ticket> OpenTickets() =>
        db.Tickets.AsNoTracking().Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

    public async Task<OverviewCounts> CountsAsync(DateTime dayStartUtc, DateTime dayEndUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var open = await OpenTickets().CountAsync(cancellationToken);
        var pending = await db.Tickets.AsNoTracking().CountAsync(t => t.Status == TicketStatus.Pending, cancellationToken);
        var breached = await OpenTickets().CountAsync(
            t => (t.ResponseDueAt != null && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= nowUtc)))
                 || (t.ResolutionDueAt != null && t.ResolutionDueAt <= nowUtc),
            cancellationToken);
        var resolvedToday = await db.Tickets.AsNoTracking().CountAsync(
            t => t.ResolvedAt != null && t.ResolvedAt >= dayStartUtc && t.ResolvedAt < dayEndUtc, cancellationToken);
        var newToday = await db.Tickets.AsNoTracking().CountAsync(
            t => t.CreatedAt >= dayStartUtc && t.CreatedAt < dayEndUtc, cancellationToken);
        return new OverviewCounts(open, pending, breached, resolvedToday, newToday);
    }

    public async Task<IReadOnlyList<OverdueTicketRow>> OverdueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
    {
        var rows = await OpenTickets()
            .Where(t => (t.ResponseDueAt != null && t.FirstResponseAt == null && t.ResponseDueAt <= nowUtc)
                        || (t.ResolutionDueAt != null && t.ResolutionDueAt <= nowUtc))
            .Select(t => new
            {
                t.Id,
                t.Prefix,
                t.Number,
                t.Subject,
                t.Priority,
                AssigneeName = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FullName).FirstOrDefault(),
                t.ResponseDueAt,
                t.FirstResponseAt,
                t.ResolutionDueAt,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(r => new OverdueTicketRow(
                    r.Id, r.Prefix, r.Number, r.Subject, r.Priority, r.AssigneeName,
                    DueTime(r.ResponseDueAt, r.FirstResponseAt, r.ResolutionDueAt, nowUtc)))
                .OrderBy(r => r.DueAt).ThenBy(r => r.Number)
                .Take(take),
        ];
    }

    /// <summary>The earliest missed due time (an unanswered response time, or the resolution time).</summary>
    private static DateTime DueTime(DateTime? responseDue, DateTime? firstResponse, DateTime? resolutionDue, DateTime nowUtc)
    {
        var missed = new List<DateTime>();
        if (responseDue is { } response && firstResponse is null && response <= nowUtc)
        {
            missed.Add(response);
        }

        if (resolutionDue is { } resolution && resolution <= nowUtc)
        {
            missed.Add(resolution);
        }

        return missed.Min();
    }

    public Task<int> CountCustomersAsync(CancellationToken cancellationToken) => db.Customers.AsNoTracking().CountAsync(cancellationToken);
}
