using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>API names of the channel enums (lower case).</summary>
public static class ChannelValues
{
    public static string ChannelName(ChannelKind channel) => channel switch
    {
        ChannelKind.Email => "email",
        ChannelKind.WhatsApp => "whatsapp",
        ChannelKind.Sms => "sms",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };

    public static string StatusName(DeliveryStatus status) => status switch
    {
        DeliveryStatus.Pending => "pending",
        DeliveryStatus.Sent => "sent",
        DeliveryStatus.Delivered => "delivered",
        DeliveryStatus.Read => "read",
        DeliveryStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
