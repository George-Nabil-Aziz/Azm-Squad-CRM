namespace Crm.Domain.Tickets;

/// <summary>Whether an outbound reply reached its channel (set by the channel stories). Stored by name.</summary>
public enum MessageDeliveryStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
}
