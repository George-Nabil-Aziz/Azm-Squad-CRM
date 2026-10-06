namespace Crm.Domain.Tickets;

/// <summary>Fixed ticket priorities (CRM-12). SLA due times are set per priority (CRM-19). Stored by name.</summary>
public enum TicketPriority
{
    High = 1,
    Mid = 2,
    Low = 3,
}
