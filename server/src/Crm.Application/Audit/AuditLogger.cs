using System.Text.Json;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.Audit;
using FluentValidation;

namespace Crm.Application.Audit;

public sealed class AuditLogger(
    IAuditLogRepository repository,
    ICurrentUser currentUser,
    IClientInfo clientInfo,
    TimeProvider timeProvider) : IAuditLogger
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var entry = AuditLogEntry.Create(
            timeProvider.GetUtcNow().UtcDateTime,
            auditEvent.UserId ?? currentUser.UserId,
            auditEvent.UserEmail,
            auditEvent.Action,
            auditEvent.EntityType,
            auditEvent.EntityId,
            Serialize(auditEvent.OldValues),
            Serialize(auditEvent.NewValues),
            clientInfo.IpAddress);

        repository.Add(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string? Serialize(object? values) => values is null ? null : JsonSerializer.Serialize(values, Json);
}

public sealed class AuditLogService(IAuditLogRepository repository, IValidator<ListAuditLogsQuery> validator) : IAuditLogService
{
    public async Task<PagedResult<AuditLogResponse>> ListAsync(ListAuditLogsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);

        var action = string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim();
        return await repository.ListAsync(
            new AuditLogFilter(query.UserId, action, query.From?.UtcDateTime, query.To?.UtcDateTime),
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);
    }
}

public sealed class ListAuditLogsQueryValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithName(_ => AuditText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => AuditText.PageSizeField);
        RuleFor(x => x.Action)
            .Must(action => AuditActions.All.Contains(action!.Trim(), StringComparer.Ordinal))
            .When(x => !string.IsNullOrWhiteSpace(x.Action))
            .WithMessage(_ => AuditText.UnknownAction);
        RuleFor(x => x.From)
            .Must((query, from) => from <= query.To)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage(_ => AuditText.FromAfterTo);
    }
}
