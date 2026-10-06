namespace Crm.Domain.Channels;

/// <summary>
/// Delivery state of an outgoing message. Ordered by progress: a later status never moves back to an earlier one
/// (<see cref="Failed"/> is the exception and always applies).
/// </summary>
public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Read,
    Failed,
}
