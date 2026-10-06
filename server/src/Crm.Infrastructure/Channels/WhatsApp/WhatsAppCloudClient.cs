using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Application.Channels;

namespace Crm.Infrastructure.Channels.WhatsApp;

/// <summary>The Cloud API answered with an error status; the message is the Graph error text.</summary>
public sealed class WhatsAppApiException(string message) : Exception(message);

/// <summary>Typed HttpClient for the WhatsApp Cloud API (<c>POST /{PhoneNumberId}/messages</c>).</summary>
public sealed class WhatsAppCloudClient(HttpClient http, WhatsAppChannelOptions options)
{
    /// <summary>Sends a text or (when <paramref name="templateName"/> is set) template message; returns the wamid.</summary>
    public async Task<string?> SendAsync(string to, string body, string? templateName, CancellationToken cancellationToken)
    {
        var digits = to.TrimStart('+');
        JsonObject payload = string.IsNullOrWhiteSpace(templateName)
            ? new()
            {
                ["messaging_product"] = "whatsapp",
                ["to"] = digits,
                ["type"] = "text",
                ["text"] = new JsonObject { ["body"] = body },
            }
            : new()
            {
                ["messaging_product"] = "whatsapp",
                ["to"] = digits,
                ["type"] = "template",
                ["template"] = new JsonObject
                {
                    ["name"] = templateName,
                    ["language"] = new JsonObject { ["code"] = options.TemplateLanguage },
                },
            };

        var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl}{options.PhoneNumberId}/messages"))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new WhatsAppApiException(ReadError(json) ?? $"WhatsApp API answered {(int)response.StatusCode}.");
        }

        return ReadMessageId(json);
    }

    private static string? ReadError(string json)
    {
        try
        {
            return JsonNode.Parse(json)?["error"]?["message"]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ReadMessageId(string json)
    {
        try
        {
            return JsonNode.Parse(json)?["messages"]?[0]?["id"]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
