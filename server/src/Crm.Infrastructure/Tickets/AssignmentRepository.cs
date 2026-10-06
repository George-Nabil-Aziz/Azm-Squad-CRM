using Crm.Application.Auth;
using Crm.Application.Tickets;
using Crm.Domain.Settings;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of the auto-assign setting and the agents' duty flag / workload (CRM-27).</summary>
public sealed class AssignmentRepository(CrmDbContext db) : IAssignmentRepository
{
    public async Task<bool> IsAutoAssignEnabledAsync(CancellationToken cancellationToken) =>
        await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == AppSetting.AutoAssignEnabledKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) == bool.TrueString;

    public async Task SetAutoAssignEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        var value = enabled ? bool.TrueString : bool.FalseString;
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSetting.AutoAssignEnabledKey, cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting(AppSetting.AutoAssignEnabledKey, value));
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignmentCandidate>> ListCandidatesAsync(CancellationToken cancellationToken) =>
        [.. (await ListAgentsAsync(cancellationToken))
            .Where(a => a.OnDuty)
            .Select(a => new AssignmentCandidate(a.Id, a.FullName, a.OpenTickets))];

    public async Task<IReadOnlyList<AssignmentAgentResponse>> ListAgentsAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && AgentIds().Contains(u.Id))
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Select(u => new AssignmentAgentResponse(
                u.Id,
                u.FullName,
                u.IsOnDuty,
                db.Tickets.Count(t => t.AssigneeId == u.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)))
            .ToListAsync(cancellationToken);

    public async Task<bool> SetOnDutyAsync(Guid userId, bool onDuty, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && AgentIds().Contains(u.Id), cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.IsOnDuty = onDuty;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Guid> AgentIds() =>
        from userRole in db.UserRoles
        join role in db.Roles on userRole.RoleId equals role.Id
        where role.Name == Roles.Agent
        select userRole.UserId;
}
