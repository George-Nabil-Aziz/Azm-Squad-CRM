using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Channels;

namespace Crm.Application.Channels.Sms;

/// <summary>
/// Settings section <c>Channels:Sms</c> (a Twilio-compatible SMS API). <c>AuthToken</c> only in user-secrets / environment
/// variables (<c>Channels__Sms__AuthToken</c>). <c>WebhookBaseUrl</c> = the public address the provider calls (e.g.
/// "https://crm.example.com"); it is part of the signed URL and of the delivery-status callback. Missing values never stop the app.
/// </summary>
public sealed class SmsChannelOptions
{
    public const string SectionName = "Channels:Sms";

    public string? AccountSid { get; set; }

    public string? AuthToken { get; set; }

    /// <summary>The sender number (E.164) or messaging service the provider sends from.</summary>
    public string? FromNumber { get; set; }

    public string ApiBaseUrl { get; set; } = "https://api.twilio.com/2010-04-01/";

    public string? WebhookBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken) && !string.IsNullOrWhiteSpace(FromNumber);
}

/// <summary>Twilio webhook signature: <c>X-Twilio-Signature</c> = base64(HMAC-SHA1(token, url + each form key and value, keys sorted)).</summary>
public static class TwilioSignature
{
    public const string HeaderName = "X-Twilio-Signature";

    public static string Compute(string url, IReadOnlyDictionary<string, string> form, string authToken)
    {
        var data = new StringBuilder(url);
        foreach (var pair in form.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            data.Append(pair.Key).Append(pair.Value);
        }

        return Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(authToken), Encoding.UTF8.GetBytes(data.ToString())));
    }

    /// <summary>True only for a matching signature (constant-time compare); false without a token or header.</summary>
    public static bool IsValid(string url, IReadOnlyDictionary<string, string> form, string? header, string? authToken)
    {
        if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(header))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Compute(url, form, authToken)), Encoding.UTF8.GetBytes(header));
    }
}

/// <summary>The SMS provider's webhooks (anonymous endpoints, protected by the signature).</summary>
public interface ISmsWebhookService
{
    /// <summary>An incoming SMS. <c>UnauthorizedException</c> (401) for a missing / wrong signature.</summary>
    Task HandleInboundAsync(string url, IReadOnlyDictionary<string, string> form, string? signature, CancellationToken cancellationToken);

    /// <summary>A delivery status callback of a message we sent.</summary>
    Task HandleStatusAsync(string url, IReadOnlyDictionary<string, string> form, string? signature, CancellationToken cancellationToken);
}

public sealed class SmsWebhookService(SmsChannelOptions options, IInboundMessageProcessor processor, IChannelSender sender, TimeProvider timeProvider)
    : ISmsWebhookService
{
    public async Task HandleInboundAsync(
        string url, IReadOnlyDictionary<string, string> form, string? signature, CancellationToken cancellationToken)
    {
        Verify(url, form, signature);
        var sid = Value(form, "MessageSid");
        var from = Value(form, "From");
        if (sid.Length == 0 || from.Length == 0)
        {
            return; // not an incoming message
        }

        var body = Value(form, "Body");
        await processor.ProcessAsync(
            new InboundChannelMessage(
                ChannelKind.Sms, sid, from, null, null, body.Length == 0 ? ChannelText.NoText : body, timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
    }

    public async Task HandleStatusAsync(
        string url, IReadOnlyDictionary<string, string> form, string? signature, CancellationToken cancellationToken)
    {
        Verify(url, form, signature);
        var sid = Value(form, "MessageSid");
        DeliveryStatus? status = Value(form, "MessageStatus").ToLowerInvariant() switch
        {
            "sent" => DeliveryStatus.Sent,
            "delivered" => DeliveryStatus.Delivered,
            "read" => DeliveryStatus.Read,
            "failed" or "undelivered" => DeliveryStatus.Failed,
            _ => null,
        };
        if (sid.Length == 0 || status is null)
        {
            return;
        }

        var error = Value(form, "ErrorMessage");
        if (error.Length == 0 && Value(form, "ErrorCode") is { Length: > 0 } code)
        {
            error = $"SMS error {code}";
        }

        await sender.ApplyDeliveryStatusAsync(sid, status.Value, error.Length == 0 ? null : error, cancellationToken);
    }

    private void Verify(string url, IReadOnlyDictionary<string, string> form, string? signature)
    {
        if (!TwilioSignature.IsValid(url, form, signature, options.AuthToken))
        {
            throw new UnauthorizedException(ChannelText.WebhookSignatureInvalid);
        }
    }

    private static string Value(IReadOnlyDictionary<string, string> form, string key) =>
        form.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
