namespace Crm.Domain.Sla;

/// <summary>What happened to a ticket's SLA (CRM-21); stored by name.</summary>
public enum SlaEventType
{
    ResponseBreached,
    ResolutionBreached,
}
