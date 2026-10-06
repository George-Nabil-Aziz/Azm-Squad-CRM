namespace Crm.Domain.Sla;

/// <summary>What happened to a ticket's SLA (CRM-21, CRM-22); stored by name.</summary>
public enum SlaEventType
{
    ResponseBreached,
    ResolutionBreached,

    /// <summary>80 % of the response time passed without a response (level 0).</summary>
    ResponseWarning,

    /// <summary>The ticket escalated to the supervisors; <c>Level</c> is the new escalation level.</summary>
    Escalated,
}
