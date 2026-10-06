namespace Crm.Domain.Channels;

/// <summary>
/// WhatsApp customer-service window: free-text messages may be sent only within 24 hours of the customer's last
/// message; outside it only an approved template may be sent (CRM-25).
/// </summary>
public static class WhatsAppWindow
{
    public static readonly TimeSpan Length = TimeSpan.FromHours(24);

    /// <summary>True while less than 24 hours have passed since the customer's last message; false when there is none.</summary>
    public static bool IsOpen(DateTime? lastCustomerMessageAt, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return lastCustomerMessageAt is { } last && utcNow - last < Length;
    }
}
