using System.Security.Cryptography;
using System.Text;

namespace Crm.Domain.Integrations;

/// <summary>The CRM events a webhook can subscribe to.</summary>
public static class WebhookEvents
{
    public const string TicketCreated = "ticket.created";
    public const string TicketResolved = "ticket.resolved";

    public static IReadOnlyList<string> All { get; } = [TicketCreated, TicketResolved];

    public static bool IsKnown(string? name) => name is not null && All.Contains(name, StringComparer.Ordinal);
}

/// <summary>HMAC-SHA256 signature of a delivery body (header <c>X-Crm-Signature: sha256=&lt;hex&gt;</c>).</summary>
public static class WebhookSignature
{
    public const string Prefix = "sha256=";

    public static string Sign(string secret, string body) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    /// <summary>Constant-time check, for receivers (and tests).</summary>
    public static bool Verify(string secret, string body, string? signature) =>
        signature is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(secret, body)), Encoding.UTF8.GetBytes(signature));
}

/// <summary>When a failed delivery is tried again: 1, 5, 30, 120 and 360 minutes after the failures; then it is given up.</summary>
public static class WebhookBackoff
{
    public static IReadOnlyList<TimeSpan> Delays { get; } =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

    /// <summary>The first try plus one retry per delay.</summary>
    public static int MaxAttempts => Delays.Count + 1;

    /// <summary>When to try again after <paramref name="failedAttempts"/> failures, or null when no tries are left.</summary>
    public static DateTime? NextAttemptAt(int failedAttempts, DateTime utcNow) =>
        failedAttempts >= 1 && failedAttempts <= Delays.Count ? utcNow + Delays[failedAttempts - 1] : null;
}

/// <summary>
/// A URL that receives signed JSON for CRM events (CRM-59). The signing secret is stored encrypted
/// (<c>ISecretProtector</c>) and shown once, when the webhook is created.
/// </summary>
public sealed class Webhook
{
    public const int NameMaxLength = 100;
    public const int UrlMaxLength = 2000;

    private Webhook()
    {
        // EF Core materializes webhooks through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    /// <summary>Comma separated <see cref="WebhookEvents"/>.</summary>
    public string Events { get; private set; } = string.Empty;

    /// <summary>The signing secret, encrypted.</summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<string> EventList => Events.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static string NewSecret() => "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static Webhook Create(string name, string url, IEnumerable<string> events, string protectedSecret, DateTime utcNow)
    {
        var webhook = new Webhook { Id = Guid.NewGuid(), ProtectedSecret = protectedSecret, IsEnabled = true, CreatedAt = utcNow };
        webhook.Update(name, url, events, utcNow);
        return webhook;
    }

    public void Update(string name, string url, IEnumerable<string> events, DateTime utcNow)
    {
        Name = name.Trim();
        Url = url.Trim();
        Events = string.Join(',', events.Distinct(StringComparer.Ordinal));
        UpdatedAt = utcNow;
    }

    public void SetEnabled(bool enabled, DateTime utcNow)
    {
        IsEnabled = enabled;
        UpdatedAt = utcNow;
    }

    public bool IsSubscribedTo(string eventName) => EventList.Contains(eventName, StringComparer.Ordinal);
}

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
}

/// <summary>One event to send to one webhook: the outbox row and, at the same time, the delivery log.</summary>
public sealed class WebhookDelivery
{
    public const int ErrorMaxLength = 500;

    private WebhookDelivery()
    {
        // EF Core materializes deliveries through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid WebhookId { get; private set; }

    public string Event { get; private set; } = string.Empty;

    /// <summary>The JSON body that is sent (and signed).</summary>
    public string Payload { get; private set; } = string.Empty;

    public WebhookDeliveryStatus Status { get; private set; }

    /// <summary>How many times sending was tried.</summary>
    public int Attempts { get; private set; }

    /// <summary>When the next try is due (only meaningful while <see cref="WebhookDeliveryStatus.Pending"/>).</summary>
    public DateTime NextAttemptAt { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string? LastError { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    public static WebhookDelivery Create(Guid id, Guid webhookId, string eventName, string payload, DateTime utcNow) => new()
    {
        Id = id,
        WebhookId = webhookId,
        Event = eventName,
        Payload = payload,
        Status = WebhookDeliveryStatus.Pending,
        NextAttemptAt = utcNow,
        CreatedAt = utcNow,
    };

    public void RecordSuccess(int statusCode, DateTime utcNow)
    {
        Attempts++;
        Status = WebhookDeliveryStatus.Delivered;
        LastStatusCode = statusCode;
        LastError = null;
        DeliveredAt = utcNow;
    }

    /// <summary>A failed try: schedules the next one by <see cref="WebhookBackoff"/>, or gives up (Failed).</summary>
    public void RecordFailure(int? statusCode, string error, DateTime utcNow)
    {
        Attempts++;
        LastStatusCode = statusCode;
        LastError = Cut(error);
        if (WebhookBackoff.NextAttemptAt(Attempts, utcNow) is { } next)
        {
            NextAttemptAt = next;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
        }
    }

    /// <summary>Gives up without sending (the webhook was disabled or deleted).</summary>
    public void Cancel(string reason)
    {
        Status = WebhookDeliveryStatus.Failed;
        LastError = Cut(reason);
    }

    private static string Cut(string text) => text.Length <= ErrorMaxLength ? text : text[..ErrorMaxLength];
}
