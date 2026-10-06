namespace Crm.Domain.Tickets;

/// <summary>Who a ticket message is from and who may see it. Stored by name.</summary>
public enum MessageDirection
{
    /// <summary>From the customer (written by a channel: email, WhatsApp, portal).</summary>
    Inbound = 1,

    /// <summary>A reply an agent sent to the customer.</summary>
    Outbound = 2,

    /// <summary>A note for the team only: never sent to and never visible to the customer.</summary>
    InternalNote = 3,
}
