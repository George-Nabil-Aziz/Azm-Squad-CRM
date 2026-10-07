using Crm.Application.Auth;
using Crm.Application.Dashboard;
using Crm.Domain.Channels;
using Crm.Domain.Chat;
using Crm.Domain.Integrations;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Crm.Infrastructure.Dashboard;

/// <summary>Counts of every module for the SuperAdmin / Admin home dashboard (read only, computed by the database).</summary>
public sealed class SystemOverviewRepository(CrmDbContext db, IConfiguration configuration) : ISystemOverviewRepository
{
    private static readonly (string Key, TicketStatus Status)[] Statuses =
    [
        ("new", TicketStatus.New), ("open", TicketStatus.Open), ("pending", TicketStatus.Pending),
        ("resolved", TicketStatus.Resolved), ("closed", TicketStatus.Closed),
    ];

    private static readonly (string Key, ChannelKind Kind)[] Channels =
    [
        ("email", ChannelKind.Email), ("whatsapp", ChannelKind.WhatsApp), ("sms", ChannelKind.Sms),
    ];

    public async Task<SystemCounts> CountsAsync(DateTime dayStartUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var customers = await db.Customers.AsNoTracking().CountAsync(cancellationToken);

        var statusCounts = await db.Tickets.AsNoTracking().GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byStatus = Statuses.Select(s => new NamedCount(s.Key, statusCounts.FirstOrDefault(c => c.Key == s.Status)?.Count ?? 0)).ToList();

        var breached = await db.Tickets.AsNoTracking()
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .CountAsync(
                t => (t.ResponseDueAt != null && ((t.FirstResponseAt != null && t.FirstResponseAt > t.ResponseDueAt) || (t.FirstResponseAt == null && t.ResponseDueAt <= nowUtc)))
                     || (t.ResolutionDueAt != null && t.ResolutionDueAt <= nowUtc),
                cancellationToken);

        var chats = new ChatCounts(
            await db.ChatSessions.AsNoTracking().CountAsync(c => c.Status == ChatStatus.Waiting, cancellationToken),
            await db.ChatSessions.AsNoTracking().CountAsync(c => c.Status == ChatStatus.Active, cancellationToken),
            await db.ChatSessions.AsNoTracking().CountAsync(c => c.StartedAt >= dayStartUtc, cancellationToken));

        var tasks = new TaskCounts(
            await db.Tasks.AsNoTracking().CountAsync(t => t.CompletedAt == null, cancellationToken),
            await db.Tasks.AsNoTracking().CountAsync(t => t.CompletedAt == null && t.DueAt < nowUtc, cancellationToken));

        var quickReplies = new QuickReplyCounts(
            await db.QuickReplies.AsNoTracking().CountAsync(q => !q.IsShared, cancellationToken),
            await db.QuickReplies.AsNoTracking().CountAsync(q => q.IsShared, cancellationToken));

        var kb = new KbCounts(
            await db.KbArticles.AsNoTracking().CountAsync(a => a.Status == KbArticleStatus.Published, cancellationToken),
            await db.KbArticles.AsNoTracking().CountAsync(a => a.Status == KbArticleStatus.Draft, cancellationToken),
            await db.KbFaqs.AsNoTracking().CountAsync(f => f.IsPublished, cancellationToken),
            await db.KbFaqs.AsNoTracking().CountAsync(f => !f.IsPublished, cancellationToken));

        var portal = new PortalCounts(
            await db.TicketSurveys.AsNoTracking().CountAsync(cancellationToken),
            await db.TicketSurveys.AsNoTracking().CountAsync(s => s.Rating != null, cancellationToken),
            await db.PortalAccounts.AsNoTracking().CountAsync(cancellationToken));

        var users = await UsersAsync(cancellationToken);

        var departments = new ActiveCounts(
            await db.Departments.AsNoTracking().CountAsync(d => d.IsActive, cancellationToken),
            await db.Departments.AsNoTracking().CountAsync(cancellationToken));
        var branches = new ActiveCounts(
            await db.Branches.AsNoTracking().CountAsync(b => b.IsActive, cancellationToken),
            await db.Branches.AsNoTracking().CountAsync(cancellationToken));

        var webForms = await db.Tickets.AsNoTracking().CountAsync(t => t.Channel == TicketChannel.WebForm, cancellationToken);

        var messages = new List<MessageCounts>();
        foreach (var (key, kind) in Channels)
        {
            messages.Add(new MessageCounts(
                key,
                await db.OutboundMessages.AsNoTracking().CountAsync(
                    m => m.Channel == kind && (m.Status == DeliveryStatus.Sent || m.Status == DeliveryStatus.Delivered || m.Status == DeliveryStatus.Read),
                    cancellationToken),
                await db.OutboundMessages.AsNoTracking().CountAsync(m => m.Channel == kind && m.Status == DeliveryStatus.Failed, cancellationToken),
                await db.ReceivedMessages.AsNoTracking().CountAsync(m => m.Channel == kind, cancellationToken)));
        }

        var apiKeys = new ApiKeyCounts(
            await db.ApiKeys.AsNoTracking().CountAsync(k => k.RevokedAt == null, cancellationToken),
            await db.ApiKeys.AsNoTracking().CountAsync(cancellationToken));

        var webhooks = new WebhookCounts(
            await db.Webhooks.AsNoTracking().CountAsync(w => w.IsEnabled, cancellationToken),
            await db.Webhooks.AsNoTracking().CountAsync(cancellationToken),
            await db.WebhookDeliveries.AsNoTracking().CountAsync(d => d.Status == WebhookDeliveryStatus.Failed, cancellationToken));

        var erp = new ErpCounts(
            !string.IsNullOrWhiteSpace(configuration["Integrations:Erp:BaseUrl"]),
            await db.ErpSyncLogs.AsNoTracking().CountAsync(l => l.Result == ErpSyncResult.Success, cancellationToken),
            await db.ErpSyncLogs.AsNoTracking().CountAsync(l => l.Result == ErpSyncResult.Failed, cancellationToken));

        return new SystemCounts(
            customers, byStatus, breached, chats, tasks, quickReplies, kb, portal, users, departments, branches, webForms, messages, apiKeys, webhooks, erp);
    }

    private async Task<UserCounts> UsersAsync(CancellationToken cancellationToken)
    {
        var roleRows = await (from userRole in db.UserRoles
                              join role in db.Roles on userRole.RoleId equals role.Id
                              join user in db.Users on userRole.UserId equals user.Id
                              where user.IsActive
                              select new { role.Name, user.IsOnDuty })
            .ToListAsync(cancellationToken);

        var byRole = new[] { Roles.SuperAdmin, Roles.Admin, Roles.Supervisor, Roles.Agent }
            .Select(name => new NamedCount(name, roleRows.Count(r => r.Name == name)))
            .ToList();
        var active = await db.Users.AsNoTracking().CountAsync(u => u.IsActive, cancellationToken);
        var agents = roleRows.Where(r => r.Name == Roles.Agent).ToList();
        return new UserCounts(byRole, active, agents.Count, agents.Count(a => a.IsOnDuty));
    }

    public async Task<IReadOnlyList<ActivityEntry>> RecentActivityAsync(int take, CancellationToken cancellationToken) =>
        await db.AuditLog.AsNoTracking()
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new ActivityEntry(a.Id, a.OccurredAt, a.UserEmail, a.Action, a.EntityType, a.EntityId))
            .ToListAsync(cancellationToken);
}
