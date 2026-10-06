using Crm.Application.Common.Paging;
using Crm.Domain.Audit;

namespace Crm.Application.Audit;

/// <summary>
/// An action to record. <see cref="OldValues"/> / <see cref="NewValues"/> are small objects serialized to JSON (never
/// secrets). The user, IP and time are added by <see cref="IAuditLogger"/>; <see cref="UserId"/> / <see cref="UserEmail"/>
/// are only set when nobody is signed in (login attempts).
/// </summary>
public sealed record AuditEvent(
    string Action,
    string EntityType,
    string? EntityId = null,
    object? OldValues = null,
    object? NewValues = null,
    Guid? UserId = null,
    string? UserEmail = null);

/// <summary>GET /api/audit-logs query string; <c>from</c> / <c>to</c> are inclusive instants (ISO 8601 with offset).</summary>
public sealed record ListAuditLogsQuery(
    Guid? UserId, string? Action, DateTimeOffset? From, DateTimeOffset? To, int? Page, int? PageSize);

/// <summary>The validated filter handed to the repository (UTC times).</summary>
public sealed record AuditLogFilter(Guid? UserId, string? Action, DateTime? From, DateTime? To);

public sealed record AuditLogResponse(
    long Id,
    DateTime OccurredAt,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress);

public interface IAuditLogger
{
    /// <summary>Writes one entry (saved immediately) with the signed-in user, the client IP and the current time.</summary>
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>The caller's network address (implemented in Crm.Api from the HTTP connection).</summary>
public interface IClientInfo
{
    string? IpAddress { get; }
}

public interface IAuditLogRepository
{
    void Add(AuditLogEntry entry);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Newest first; the user's email is the typed one (login) or the user's current email.</summary>
    Task<PagedResult<AuditLogResponse>> ListAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IAuditLogService
{
    Task<PagedResult<AuditLogResponse>> ListAsync(ListAuditLogsQuery query, CancellationToken cancellationToken);
}
