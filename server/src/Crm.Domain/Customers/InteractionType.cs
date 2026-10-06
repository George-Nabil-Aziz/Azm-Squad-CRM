namespace Crm.Domain.Customers;

/// <summary>Category of a timeline entry; the timeline can be filtered by it (stored by name).</summary>
public enum InteractionType
{
    /// <summary>The customer profile itself: created, updated, contact added.</summary>
    Customer,

    /// <summary>A note an agent wrote about the customer (CRM-11).</summary>
    Note,

    /// <summary>A file attached to the customer (CRM-11).</summary>
    Attachment,

    /// <summary>A ticket of the customer (CRM-13 and later ticket stories).</summary>
    Ticket,

    /// <summary>A message from or to the customer (CRM-15, CRM-23..26).</summary>
    Message,
}
