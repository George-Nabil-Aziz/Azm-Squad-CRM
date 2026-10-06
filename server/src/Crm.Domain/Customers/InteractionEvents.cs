namespace Crm.Domain.Customers;

/// <summary>
/// Codes of the things a timeline entry can say happened. The client translates them
/// (<c>customers.timeline.events.&lt;code&gt;</c>); later stories add their own codes here.
/// </summary>
public static class InteractionEvents
{
    public const string CustomerCreated = "customerCreated";

    public const string CustomerUpdated = "customerUpdated";

    public const string ContactAdded = "contactAdded";

    public const string NoteAdded = "noteAdded";

    public const string AttachmentAdded = "attachmentAdded";

    /// <summary>A ticket was created for the customer (CRM-13); details "TKT-000001 &lt;subject&gt;", source = the ticket.</summary>
    public const string TicketCreated = "ticketCreated";

    /// <summary>An agent replied to the customer on a ticket (CRM-15); details = start of the reply, source = the message.</summary>
    public const string MessageSent = "messageSent";

    /// <summary>A customer message arrived on a ticket through a channel (CRM-24 / CRM-26); details = start of the text, source = the ticket message.</summary>
    public const string MessageReceived = "messageReceived";
}
