using Crm.Application.Audit;
using Crm.Application.Common.Paging;
using Crm.Domain.Audit;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Audit;

/// <summary>EF Core storage of the audit log (append and filtered, paged reading; there is no update or delete).</summary>
public sealed class AuditLogRepository(CrmDbContext db) : IAuditLogRepository
{
    public void Add(AuditLogEntry entry) => db.AuditLog.Add(entry);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<PagedResult<AuditLogResponse>> ListAsync(
        AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var entries = db.AuditLog.AsNoTracking().AsQueryable();
        if (filter.UserId is { } userId)
        {
            entries = entries.Where(e => e.UserId == userId);
        }

        if (filter.Action is { } action)
        {
            entries = entries.Where(e => e.Action == action);
        }

        if (filter.From is { } from)
        {
            entries = entries.Where(e => e.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            entries = entries.Where(e => e.OccurredAt <= to);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var items = await entries
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new AuditLogResponse(
                e.Id,
                e.OccurredAt,
                e.UserId,
                e.UserEmail ?? db.Users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefault(),
                e.Action,
                e.EntityType,
                e.EntityId,
                e.OldValues,
                e.NewValues,
                e.IpAddress))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogResponse>(items, page, pageSize, totalCount);
    }
}
