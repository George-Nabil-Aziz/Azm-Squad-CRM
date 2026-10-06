using Crm.Application.Audit;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Domain.Audit;

namespace Crm.UnitTests.Audit;

/// <summary>Records the events a service logs (the real logger writes them to the database).</summary>
internal sealed class FakeAuditLogger : IAuditLogger
{
    public List<AuditEvent> Events { get; } = [];

    public Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

internal sealed class AuditActor(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => false;
}

internal sealed class FakeClientInfo(string? ipAddress) : IClientInfo
{
    public string? IpAddress { get; } = ipAddress;
}

internal sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLogEntry> Added { get; } = [];

    public int SaveCount { get; private set; }

    public (AuditLogFilter Filter, int Page, int PageSize)? LastList { get; private set; }

    public void Add(AuditLogEntry entry) => Added.Add(entry);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditLogResponse>> ListAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (filter, page, pageSize);
        return Task.FromResult(new PagedResult<AuditLogResponse>([], page, pageSize, 0));
    }
}

internal sealed class AuditClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
