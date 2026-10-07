using Crm.Application.Auth;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Application.Dashboard;

/// <summary>A ticket past an SLA due time that is still not resolved or closed.</summary>
public sealed record OverdueTicketResponse(
    Guid TicketId, string Number, string Subject, string Priority, string? AssigneeName, DateTime DueAt);

/// <summary>
/// The staff home dashboard counts of all tickets. <c>TotalCustomers</c> is null for a user without
/// <c>customers.view</c>. Open = not Resolved or Closed; BreachedNow = open tickets with a missed response or resolution time.
/// </summary>
public sealed record DashboardOverviewResponse(
    DateTime GeneratedAt,
    int OpenTickets,
    int PendingTickets,
    int BreachedNow,
    int ResolvedToday,
    int NewToday,
    int? TotalCustomers,
    IReadOnlyList<OverdueTicketResponse> Overdue);

public sealed record OverdueTicketRow(Guid Id, string Prefix, int Number, string Subject, TicketPriority Priority, string? AssigneeName, DateTime DueAt);

public sealed record OverviewCounts(int Open, int Pending, int BreachedNow, int ResolvedToday, int NewToday);

public interface IDashboardOverviewRepository
{
    Task<OverviewCounts> CountsAsync(DateTime dayStartUtc, DateTime dayEndUtc, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Open tickets past a due time, the longest overdue first.</summary>
    Task<IReadOnlyList<OverdueTicketRow>> OverdueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

    Task<int> CountCustomersAsync(CancellationToken cancellationToken);
}

public interface IDashboardOverviewService
{
    Task<DashboardOverviewResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed class DashboardOverviewService(IDashboardOverviewRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
    : IDashboardOverviewService
{
    public const int OverdueLimit = 5;

    public async Task<DashboardOverviewResponse> GetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var counts = await repository.CountsAsync(dayStart, dayStart.AddDays(1), now, cancellationToken);
        var overdue = await repository.OverdueAsync(now, OverdueLimit, cancellationToken);
        int? customers = currentUser.HasPermission(Permissions.CustomersView) ? await repository.CountCustomersAsync(cancellationToken) : null;

        return new DashboardOverviewResponse(
            now, counts.Open, counts.Pending, counts.BreachedNow, counts.ResolvedToday, counts.NewToday, customers,
            [.. overdue.Select(r => new OverdueTicketResponse(
                r.Id, Ticket.FormatNumber(r.Number, r.Prefix), r.Subject, TicketValues.PriorityName(r.Priority), r.AssigneeName, r.DueAt))]);
    }
}
