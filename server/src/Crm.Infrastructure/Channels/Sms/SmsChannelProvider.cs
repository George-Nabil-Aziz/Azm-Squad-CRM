using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Crm.Application.Channels;
using Crm.Application.Channels.Sms;
using Crm.Domain.Channels;

namespace Crm.Infrastructure.Channels.Sms;

/// <summary>The SMS API answered with an error status; the message is the provider's error text.</summary>
public sealed class SmsApiException(string message) : Exception(message);

/// <summary>Typed HttpClient for a Twilio-compatible SMS API (<c>POST Accounts/{sid}/Messages.json</c>, basic auth, form body).</summary>
public sealed class TwilioSmsClient(HttpClient http, SmsChannelOptions options)
{
    /// <summary>Sends the text; returns the provider's message id (sid).</summary>
    public async Task<string?> SendAsync(string to, string body, CancellationToken cancellationToken)
    {
        var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";
        var form = new Dictionary<string, string> { ["To"] = to, ["From"] = options.FromNumber!, ["Body"] = body };
        if (!string.IsNullOrWhiteSpace(options.WebhookBaseUrl))
        {
            form["StatusCallback"] = options.WebhookBaseUrl.TrimEnd('/') + "/api/webhooks/sms/status";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl}Accounts/{options.AccountSid}/Messages.json"))
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}")));

        using var response = await http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SmsApiException(Read(json, "message") ?? $"SMS API answered {(int)response.StatusCode}.");
        }

        return Read(json, "sid");
    }

    private static string? Read(string json, string property)
    {
        try
        {
            return JsonNode.Parse(json)?[property]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>The SMS channel: sends replies through the SMS API. Settings: <c>Channels:Sms</c>.</summary>
public sealed class SmsChannelProvider(SmsChannelOptions options, TwilioSmsClient client) : IChannelProvider
{
    public ChannelKind Channel => ChannelKind.Sms;

    public bool IsConfigured => options.IsConfigured;

    public async Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ChannelSendResult.Fail(ChannelText.SmsNotConfigured);
        }

        try
        {
            return ChannelSendResult.Ok(await client.SendAsync(message.Recipient, message.Body, cancellationToken));
        }
        catch (Exception exception) when (exception is SmsApiException or HttpRequestException or TaskCanceledException)
        {
            // API error, network error or timeout: stored on the message and retried with back-off.
            return ChannelSendResult.Fail(exception.Message);
        }
    }
}
