using System.Text;

namespace Crm.UnitTests.Channels;

/// <summary>WhatsApp Cloud API webhook bodies (shape of Meta's "messages" field notifications).</summary>
internal static class WhatsAppPayloads
{
    public static byte[] Text(string messageId, string from, string? profileName, string text, long timestamp) =>
        Encoding.UTF8.GetBytes($$"""
            {
              "object": "whatsapp_business_account",
              "entry": [{
                "id": "WABA_ID",
                "changes": [{
                  "field": "messages",
                  "value": {
                    "messaging_product": "whatsapp",
                    "metadata": { "display_phone_number": "15550001111", "phone_number_id": "PHONE_ID" },
                    "contacts": [{ {{(profileName is null ? "" : $"\"profile\": {{ \"name\": \"{profileName}\" }},")}} "wa_id": "{{from}}" }],
                    "messages": [{ "from": "{{from}}", "id": "{{messageId}}", "timestamp": "{{timestamp}}", "type": "text", "text": { "body": "{{text}}" } }]
                  }
                }]
              }]
            }
            """);

    public static byte[] Image(string messageId, string from, long timestamp) =>
        Encoding.UTF8.GetBytes($$"""
            {
              "object": "whatsapp_business_account",
              "entry": [{ "id": "WABA_ID", "changes": [{ "field": "messages", "value": {
                "messaging_product": "whatsapp",
                "contacts": [{ "wa_id": "{{from}}" }],
                "messages": [{ "from": "{{from}}", "id": "{{messageId}}", "timestamp": "{{timestamp}}", "type": "image", "image": { "id": "MEDIA_ID" } }]
              } }] }]
            }
            """);

    public static byte[] Status(string messageId, string status, long timestamp, string? errorTitle = null) =>
        Encoding.UTF8.GetBytes($$"""
            {
              "object": "whatsapp_business_account",
              "entry": [{ "id": "WABA_ID", "changes": [{ "field": "messages", "value": {
                "messaging_product": "whatsapp",
                "statuses": [{ "id": "{{messageId}}", "status": "{{status}}", "timestamp": "{{timestamp}}", "recipient_id": "966501234567"
                  {{(errorTitle is null ? "" : $", \"errors\": [{{ \"code\": 131047, \"title\": \"{errorTitle}\" }}]")}} }]
              } }] }]
            }
            """);
}
