namespace Crm.Domain.Tickets;

/// <summary>How a ticket reached the CRM: created by an agent (Manual) or from a channel (CRM-23..26, portal). Stored by name.</summary>
public enum TicketChannel
{
    Manual = 1,
    Email = 2,
    WhatsApp = 3,
    Portal = 4,
    WebForm = 5,
    Chat = 6,
    Sms = 7,
}
