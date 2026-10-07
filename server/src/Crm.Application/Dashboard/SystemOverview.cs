using Crm.Application.Ai;
using Crm.Application.Auth;
using Crm.Application.Common.Security;

namespace Crm.Application.Dashboard;

/// <summary>A named count (status, role, ...).</summary>
public sealed record NamedCount(string Key, int Count);

public sealed record ChatCounts(int Waiting, int Active, int Today);

public sealed record TaskCounts(int Open, int Overdue);

public sealed record QuickReplyCounts(int Personal, int Shared);

public sealed record KbCounts(int PublishedArticles, int DraftArticles, int PublishedFaqs, int DraftFaqs);

public sealed record PortalCounts(int SurveysSent, int SurveysAnswered, int Accounts);

public sealed record UserCounts(IReadOnlyList<NamedCount> ByRole, int Active, int ActiveAgents, int OnDutyAgents);

public sealed record ActiveCounts(int Active, int Total);

public sealed record MessageCounts(string Channel, int Sent, int Failed, int Received);

public sealed record ApiKeyCounts(int Active, int Total);

public sealed record WebhookCounts(int Enabled, int Total, int FailedDeliveries);

public sealed record ErpCounts(bool Configured, int Synced, int Failed);

public sealed record AiCounts(bool Configured);

public sealed record ActivityEntry(long Id, DateTime OccurredAt, string? UserEmail, string Action, string EntityType, string? EntityId);

/// <summary>What the repository counts; the service adds the settings-based facts and the permission-based parts.</summary>
public sealed record SystemCounts(
    int Customers,
    IReadOnlyList<NamedCount> TicketsByStatus,
    int SlaBreachedNow,
    ChatCounts Chats,
    TaskCounts Tasks,
    QuickReplyCounts QuickReplies,
    KbCounts Kb,
    PortalCounts Portal,
    UserCounts Users,
    ActiveCounts Departments,
    ActiveCounts Branches,
    int WebFormSubmissions,
    IReadOnlyList<MessageCounts> Messages,
    ApiKeyCounts ApiKeys,
    WebhookCounts Webhooks,
    ErpCounts Erp);

/// <summary>GET /api/dashboard/system-overview: counts of every module for SuperAdmin and Admin. RecentActivity is null without audit.view.</summary>
public sealed record SystemOverviewResponse(
    DateTime GeneratedAt,
    int Customers,
    IReadOnlyList<NamedCount> TicketsByStatus,
    int SlaBreachedNow,
    ChatCounts Chats,
    TaskCounts Tasks,
    QuickReplyCounts QuickReplies,
    KbCounts Kb,
    PortalCounts Portal,
    UserCounts Users,
    ActiveCounts Departments,
    ActiveCounts Branches,
    int WebFormSubmissions,
    IReadOnlyList<MessageCounts> Messages,
    ApiKeyCounts ApiKeys,
    WebhookCounts Webhooks,
    ErpCounts Erp,
    AiCounts Ai,
    IReadOnlyList<ActivityEntry>? RecentActivity);

public interface ISystemOverviewRepository
{
    Task<SystemCounts> CountsAsync(DateTime dayStartUtc, DateTime nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityEntry>> RecentActivityAsync(int take, CancellationToken cancellationToken);
}

public interface ISystemOverviewService
{
    Task<SystemOverviewResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed class SystemOverviewService(
    ISystemOverviewRepository repository, ICurrentUser currentUser, AiOptions ai, TimeProvider timeProvider) : ISystemOverviewService
{
    public const int ActivityLimit = 8;

    public async Task<SystemOverviewResponse> GetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var c = await repository.CountsAsync(dayStart, now, cancellationToken);
        var activity = currentUser.HasPermission(Permissions.AuditView) ? await repository.RecentActivityAsync(ActivityLimit, cancellationToken) : null;

        return new SystemOverviewResponse(
            now, c.Customers, c.TicketsByStatus, c.SlaBreachedNow, c.Chats, c.Tasks, c.QuickReplies, c.Kb, c.Portal, c.Users,
            c.Departments, c.Branches, c.WebFormSubmissions, c.Messages, c.ApiKeys, c.Webhooks, c.Erp, new AiCounts(ai.IsConfigured), activity);
    }
}
