using Crm.Application.Common.Exceptions;
using Crm.Application.Notifications;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>An agent who may receive an automatic assignment, with the tickets they are working on.</summary>
public sealed record AssignmentCandidate(Guid Id, string Name, int OpenTickets);

/// <summary>The rule of CRM-27: the candidate with the fewest open tickets; ties by name, then id (deterministic).</summary>
public static class AutoAssignmentRules
{
    public static AssignmentCandidate? Pick(IEnumerable<AssignmentCandidate> candidates) =>
        candidates.OrderBy(c => c.OpenTickets)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
}

/// <summary>An agent shown on the assignment settings: <c>OnDuty</c> = may receive automatic assignments.</summary>
public sealed record AssignmentAgentResponse(Guid Id, string FullName, bool OnDuty, int OpenTickets);

/// <summary>GET /api/settings/assignment: the on / off setting and the agents with their duty flag and workload.</summary>
public sealed record AssignmentSettingsResponse(bool AutoAssignEnabled, IReadOnlyList<AssignmentAgentResponse> Agents);

/// <summary>Body of PUT /api/settings/assignment.</summary>
public sealed record UpdateAssignmentSettingsRequest(bool? AutoAssignEnabled);

/// <summary>Body of PUT /api/settings/assignment/agents/{userId}.</summary>
public sealed record SetAgentDutyRequest(bool? OnDuty);

/// <summary>Storage of the assignment setting and of the agents' workload (implemented with EF Core).</summary>
public interface IAssignmentRepository
{
    Task<bool> IsAutoAssignEnabledAsync(CancellationToken cancellationToken);

    Task SetAutoAssignEnabledAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Active, on-duty users with the Agent role and their open tickets (not Resolved / Closed).</summary>
    Task<IReadOnlyList<AssignmentCandidate>> ListCandidatesAsync(CancellationToken cancellationToken);

    /// <summary>Every active user with the Agent role (on duty or not) with their workload.</summary>
    Task<IReadOnlyList<AssignmentAgentResponse>> ListAgentsAsync(CancellationToken cancellationToken);

    /// <summary>False when there is no active Agent with this id.</summary>
    Task<bool> SetOnDutyAsync(Guid userId, bool onDuty, CancellationToken cancellationToken);
}

/// <summary>Assignment settings use cases. <c>ValidationException</c> 400 for a missing flag, <c>NotFoundException</c> 404 for an unknown agent.</summary>
public interface IAssignmentSettingsService
{
    Task<AssignmentSettingsResponse> GetAsync(CancellationToken cancellationToken);

    Task<AssignmentSettingsResponse> UpdateAsync(UpdateAssignmentSettingsRequest request, CancellationToken cancellationToken);

    Task<AssignmentSettingsResponse> SetOnDutyAsync(Guid userId, SetAgentDutyRequest request, CancellationToken cancellationToken);
}

public sealed class AssignmentSettingsService(IAssignmentRepository repository) : IAssignmentSettingsService
{
    public async Task<AssignmentSettingsResponse> GetAsync(CancellationToken cancellationToken) =>
        new(await repository.IsAutoAssignEnabledAsync(cancellationToken), await repository.ListAgentsAsync(cancellationToken));

    public async Task<AssignmentSettingsResponse> UpdateAsync(UpdateAssignmentSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.AutoAssignEnabled is not { } enabled)
        {
            throw Missing("autoAssignEnabled");
        }

        await repository.SetAutoAssignEnabledAsync(enabled, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<AssignmentSettingsResponse> SetOnDutyAsync(Guid userId, SetAgentDutyRequest request, CancellationToken cancellationToken)
    {
        if (request.OnDuty is not { } onDuty)
        {
            throw Missing("onDuty");
        }

        if (!await repository.SetOnDutyAsync(userId, onDuty, cancellationToken))
        {
            throw new NotFoundException(TicketText.AgentNotFound);
        }

        return await GetAsync(cancellationToken);
    }

    private static ValidationException Missing(string field) =>
        new(new Dictionary<string, string[]> { [field] = [TicketText.SettingRequired] });
}

/// <summary>Gives a new ticket to the least loaded available agent when auto-assign is on (CRM-27).</summary>
public interface IAutoAssignmentService
{
    /// <summary>
    /// Assigns the (not yet saved) ticket and records the history entry (saved with the ticket). Returns the agent, or null
    /// when auto-assign is off, nobody is available or the lookup failed (the ticket then stays unassigned).
    /// </summary>
    Task<Guid?> TryAssignAsync(Ticket ticket, DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>After the ticket was saved: tells the agent about the automatic assignment (CRM-28).</summary>
    Task NotifyAssignedAsync(Guid ticketId, Guid agentId, DateTime utcNow, CancellationToken cancellationToken);
}

public sealed class AutoAssignmentService(
    IAssignmentRepository repository,
    ITicketHistoryRecorder history,
    INotificationDispatcher? notifications = null) : IAutoAssignmentService
{
    public async Task NotifyAssignedAsync(Guid ticketId, Guid agentId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (notifications is not null)
        {
            await notifications.NotifyAsync(AssignmentNotifications.For(ticketId, agentId, utcNow), cancellationToken);
        }
    }

    public async Task<Guid?> TryAssignAsync(Ticket ticket, DateTime utcNow, CancellationToken cancellationToken)
    {
        try
        {
            if (!await repository.IsAutoAssignEnabledAsync(cancellationToken))
            {
                return null;
            }

            if (AutoAssignmentRules.Pick(await repository.ListCandidatesAsync(cancellationToken)) is not { } agent)
            {
                return null;
            }

            ticket.AssignTo(agent.Id, utcNow);
            history.Record(ticket.Id, TicketHistoryField.Assignee, null, agent.Name, utcNow, system: true);
            return agent.Id;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null; // automation must never block ticket creation
        }
    }
}
