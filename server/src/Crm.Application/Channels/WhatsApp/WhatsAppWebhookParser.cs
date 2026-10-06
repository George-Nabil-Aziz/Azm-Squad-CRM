using System.Globalization;
using System.Text.Json;
using Crm.Domain.Channels;

namespace Crm.Application.Channels.WhatsApp;

/// <summary>A delivery status of a message we sent (webhook "statuses").</summary>
public sealed record WhatsAppStatusUpdate(string MessageId, DeliveryStatus Status, DateTime Timestamp, string? Error);

/// <summary>What a webhook call contains: customer messages and statuses of our messages.</summary>
public sealed record WhatsAppWebhookPayload(IReadOnlyList<InboundChannelMessage> Messages, IReadOnlyList<WhatsAppStatusUpdate> Statuses)
{
    public static WhatsAppWebhookPayload Empty { get; } = new([], []);
}

/// <summary>
/// Reads WhatsApp Cloud API webhook bodies (<c>entry[].changes[].value.messages[] / statuses[]</c>). Never throws:
/// unknown or invalid content gives an empty payload (the webhook still answers 200 so Meta does not retry).
/// </summary>
public static class WhatsAppWebhookParser
{
    public static WhatsAppWebhookPayload Parse(ReadOnlySpan<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return WhatsAppWebhookPayload.Empty;
        }
    }

    private static WhatsAppWebhookPayload Read(JsonElement root)
    {
        List<InboundChannelMessage> messages = [];
        List<WhatsAppStatusUpdate> statuses = [];
        foreach (var value in Values(root))
        {
            var names = Names(value);
            foreach (var message in Array(value, "messages"))
            {
                if (ReadMessage(message, names) is { } inbound)
                {
                    messages.Add(inbound);
                }
            }

            foreach (var status in Array(value, "statuses"))
            {
                if (ReadStatus(status) is { } update)
                {
                    statuses.Add(update);
                }
            }
        }

        return new WhatsAppWebhookPayload(messages, statuses);
    }

    private static IEnumerable<JsonElement> Values(JsonElement root) =>
        from entry in Array(root, "entry")
        from change in Array(entry, "changes")
        where change.ValueKind == JsonValueKind.Object && change.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object
        select change.GetProperty("value");

    /// <summary>wa_id → profile name of the senders in this change.</summary>
    private static Dictionary<string, string> Names(JsonElement value)
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        foreach (var contact in Array(value, "contacts"))
        {
            if (String(contact, "wa_id") is { } waId
                && contact.TryGetProperty("profile", out var profile) && String(profile, "name") is { } name)
            {
                names[waId] = name;
            }
        }

        return names;
    }

    private static InboundChannelMessage? ReadMessage(JsonElement message, Dictionary<string, string> names)
    {
        var id = String(message, "id");
        var from = String(message, "from");
        if (id is null || from is null)
        {
            return null;
        }

        var type = String(message, "type") ?? "unknown";
        var body = type == "text" && message.TryGetProperty("text", out var text) ? String(text, "body") ?? string.Empty : $"[{type} message]";
        var number = from.StartsWith('+') ? from : "+" + from;

        return new InboundChannelMessage(
            ChannelKind.WhatsApp, id, number, names.GetValueOrDefault(from), null, body, Time(message));
    }

    private static WhatsAppStatusUpdate? ReadStatus(JsonElement status)
    {
        var id = String(status, "id");
        DeliveryStatus? kind = String(status, "status") switch
        {
            "sent" => DeliveryStatus.Sent,
            "delivered" => DeliveryStatus.Delivered,
            "read" => DeliveryStatus.Read,
            "failed" => DeliveryStatus.Failed,
            _ => null,
        };
        if (id is null || kind is null)
        {
            return null;
        }

        var error = Array(status, "errors").Select(e => String(e, "title") ?? String(e, "message")).FirstOrDefault(e => e is not null);
        return new WhatsAppStatusUpdate(id, kind.Value, Time(status), error);
    }

    /// <summary>Unix "timestamp" (seconds, as a string) in UTC; the Unix epoch when missing.</summary>
    private static DateTime Time(JsonElement element) =>
        long.TryParse(String(element, "timestamp"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : DateTime.UnixEpoch;

    private static IEnumerable<JsonElement> Array(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
