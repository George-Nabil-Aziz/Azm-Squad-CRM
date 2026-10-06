using System.Linq.Expressions;
using Crm.Application.Common.Paging;
using Crm.Application.Notifications;
using Crm.Domain.Notifications;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Notifications;

/// <summary>EF Core storage of notifications.</summary>
public sealed class NotificationRepository(CrmDbContext db) : INotificationRepository
{
    public void Add(Notification notification) => db.Notifications.Add(notification);

    public Task<bool> ExistsAsync(Guid userId, string dedupKey, CancellationToken cancellationToken) =>
        db.Notifications.AnyAsync(n => n.RecipientUserId == userId && n.DedupKey == dedupKey, cancellationToken);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another process stored the same event first (unique index): forget the rows of this attempt.
            foreach (var entry in db.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetResponsesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await Rows().Where(r => ids.Contains(r.Notification.Id)).ToListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<PagedResult<NotificationResponse>> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = Rows().Where(r => r.Notification.RecipientUserId == userId);
        if (unreadOnly)
        {
            rows = rows.Where(r => r.Notification.ReadAt == null);
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(r => r.Notification.CreatedAt).ThenByDescending(r => r.Notification.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<NotificationResponse>([.. items.Select(ToResponse)], page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, cancellationToken);

    public Task<Notification?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId, cancellationToken);

    public Task SaveReadAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = await db.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.MarkRead(utcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    private IQueryable<Row> Rows() =>
        from notification in db.Notifications.AsNoTracking()
        join ticket in db.Tickets.AsNoTracking() on notification.TicketId equals ticket.Id into tickets
        from ticket in tickets.DefaultIfEmpty()
        select new Row { Notification = notification, TicketNumber = ticket != null ? (int?)ticket.Number : null };

    private static NotificationResponse ToResponse(Row row) => new(
        row.Notification.Id,
        NotificationTypes.Name(row.Notification.Type),
        row.Notification.TicketId,
        row.TicketNumber is { } number ? Ticket.FormatNumber(number) : null,
        row.Notification.Level,
        row.Notification.Text,
        row.Notification.CreatedAt,
        row.Notification.ReadAt);

    private sealed class Row
    {
        public required Notification Notification { get; init; }

        public int? TicketNumber { get; init; }
    }
}
