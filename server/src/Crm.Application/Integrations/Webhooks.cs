using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Settings;
using Crm.Domain.Integrations;
using Crm.Domain.Tickets;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Integrations;

/// <summary>Body of POST / PUT /api/webhooks: a name, an absolute http(s) URL and at least one event (<see cref="WebhookEvents"/>).</summary>
public sealed record WebhookRequest(string? Name, string? Url, IReadOnlyList<string>? Events);

public sealed record WebhookResponse(
    Guid Id, string Name, string Url, IReadOnlyList<string> Events, bool IsEnabled, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>The response of creating a webhook: the only time <c>Secret</c> is returned.</summary>
public sealed record CreatedWebhookResponse(
    Guid Id, string Name, string Url, IReadOnlyList<string> Events, bool IsEnabled, DateTime CreatedAt, DateTime UpdatedAt, string Secret);

/// <summary>One row of the delivery log of a webhook.</summary>
public sealed record WebhookDeliveryResponse(
    Guid Id, string Event, string Status, int Attempts, DateTime NextAttemptAt, int? LastStatusCode, string? LastError,
    DateTime CreatedAt, DateTime? DeliveredAt);

/// <summary>The ticket data sent with <c>ticket.created</c> / <c>ticket.resolved</c>.</summary>
public sealed record WebhookTicketData(
    Guid Id, string Number, string Subject, string Status, string Priority, string Channel, Guid CustomerId,
    DateTime CreatedAt, DateTime? ResolvedAt)
{
    public static WebhookTicketData From(Ticket ticket) => new(
        ticket.Id, ticket.DisplayNumber, ticket.Subject, ticket.Status.ToString().ToLowerInvariant(),
        ticket.Priority.ToString().ToLowerInvariant(), ticket.Channel.ToString().ToLowerInvariant(), ticket.CustomerId,
        ticket.CreatedAt, ticket.ResolvedAt);
}

/// <summary>A delivery with the webhook it belongs to (what the delivery job needs).</summary>
public sealed record WebhookDeliveryWork(WebhookDelivery Delivery, Webhook? Webhook);

/// <summary>The outcome of one HTTP call to a receiver.</summary>
public sealed record WebhookSendResult(bool Success, int? StatusCode, string? Error);

/// <summary>Sends one delivery over HTTP (implemented in Crm.Infrastructure with <c>HttpClient</c>; tests use a fake).</summary>
public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(
        string url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken);
}

/// <summary>Webhook and delivery storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IWebhookRepository
{
    void Add(Webhook webhook);

    void Remove(Webhook webhook);

    void AddDelivery(WebhookDelivery delivery);

    Task<Webhook?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Webhook>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Enabled webhooks that subscribe to the event.</summary>
    Task<IReadOnlyList<Webhook>> ListEnabledForEventAsync(string eventName, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(Guid webhookId, int take, CancellationToken cancellationToken);

    /// <summary>Pending deliveries whose next attempt is due, oldest first (tracked).</summary>
    Task<IReadOnlyList<WebhookDeliveryWork>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken);

    /// <summary>Pending deliveries of a webhook (tracked), to cancel them when it is disabled.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListPendingAsync(Guid webhookId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Webhook administration (<c>integrations.manage</c>, enforced by the API). <c>ValidationException</c> 400, <c>NotFoundException</c> 404.
/// </summary>
public interface IWebhookService
{
    Task<CreatedWebhookResponse> CreateAsync(WebhookRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookResponse>> ListAsync(CancellationToken cancellationToken);

    Task<WebhookResponse> UpdateAsync(Guid id, WebhookRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Disabling stops all deliveries: new events are not queued and the pending ones are cancelled.</summary>
    Task<WebhookResponse> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookDeliveryResponse>> ListDeliveriesAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Queues an event for every enabled webhook that subscribes to it. Never throws: a webhook problem must not break the ticket.</summary>
public interface IWebhookEventPublisher
{
    Task PublishAsync(string eventName, object data, CancellationToken cancellationToken);
}

public sealed class WebhookRequestValidator : AbstractValidator<WebhookRequest>
{
    public WebhookRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(_ => IntegrationText.NameRequired).MaximumLength(Webhook.NameMaxLength);
        RuleFor(x => x.Url).NotEmpty().WithMessage(_ => IntegrationText.UrlInvalid)
            .MaximumLength(Webhook.UrlMaxLength)
            .Must(IsHttpUrl).WithMessage(_ => IntegrationText.UrlInvalid);
        RuleFor(x => x.Events).NotEmpty().WithMessage(_ => IntegrationText.EventsRequired);
        RuleForEach(x => x.Events).Must(WebhookEvents.IsKnown).WithMessage((_, name) => IntegrationText.EventUnknown(name));
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class WebhookService(
    IWebhookRepository webhooks, ISecretProtector protector, TimeProvider timeProvider, IValidator<WebhookRequest> validator)
    : IWebhookService
{
    public async Task<CreatedWebhookResponse> CreateAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var secret = Webhook.NewSecret();
        var webhook = Webhook.Create(request.Name!, request.Url!, request.Events!, protector.Protect(secret), Now());
        webhooks.Add(webhook);
        await webhooks.SaveChangesAsync(cancellationToken);
        return new CreatedWebhookResponse(
            webhook.Id, webhook.Name, webhook.Url, webhook.EventList, webhook.IsEnabled, webhook.CreatedAt, webhook.UpdatedAt, secret);
    }

    public async Task<IReadOnlyList<WebhookResponse>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await webhooks.ListAsync(cancellationToken)).Select(ToResponse)];

    public async Task<WebhookResponse> UpdateAsync(Guid id, WebhookRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var webhook = await FindAsync(id, cancellationToken);
        webhook.Update(request.Name!, request.Url!, request.Events!, Now());
        await webhooks.SaveChangesAsync(cancellationToken);
        return ToResponse(webhook);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var webhook = await FindAsync(id, cancellationToken);
        foreach (var pending in await webhooks.ListPendingAsync(id, cancellationToken))
        {
            pending.Cancel(IntegrationText.WebhookDeleted);
        }

        webhooks.Remove(webhook);
        await webhooks.SaveChangesAsync(cancellationToken);
    }

    public async Task<WebhookResponse> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var webhook = await FindAsync(id, cancellationToken);
        webhook.SetEnabled(enabled, Now());
        if (!enabled)
        {
            foreach (var pending in await webhooks.ListPendingAsync(id, cancellationToken))
            {
                pending.Cancel(IntegrationText.WebhookDisabled);
            }
        }

        await webhooks.SaveChangesAsync(cancellationToken);
        return ToResponse(webhook);
    }

    public async Task<IReadOnlyList<WebhookDeliveryResponse>> ListDeliveriesAsync(Guid id, CancellationToken cancellationToken)
    {
        await FindAsync(id, cancellationToken);
        return [.. (await webhooks.ListDeliveriesAsync(id, 100, cancellationToken)).Select(d => new WebhookDeliveryResponse(
            d.Id, d.Event, d.Status.ToString().ToLowerInvariant(), d.Attempts, d.NextAttemptAt, d.LastStatusCode, d.LastError,
            d.CreatedAt, d.DeliveredAt))];
    }

    private async Task<Webhook> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await webhooks.FindAsync(id, cancellationToken) ?? throw new NotFoundException(IntegrationText.WebhookNotFound);

    private static WebhookResponse ToResponse(Webhook w) => new(w.Id, w.Name, w.Url, w.EventList, w.IsEnabled, w.CreatedAt, w.UpdatedAt);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}

public sealed class WebhookEventPublisher(IWebhookRepository webhooks, TimeProvider timeProvider, ILogger<WebhookEventPublisher> logger)
    : IWebhookEventPublisher
{
    public async Task PublishAsync(string eventName, object data, CancellationToken cancellationToken)
    {
        try
        {
            var targets = await webhooks.ListEnabledForEventAsync(eventName, cancellationToken);
            if (targets.Count == 0)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var webhook in targets)
            {
                var deliveryId = Guid.NewGuid();
                var body = JsonSerializer.Serialize(
                    new { id = deliveryId, @event = eventName, occurredAt = now, data }, JsonSerializerOptions.Web);
                webhooks.AddDelivery(WebhookDelivery.Create(deliveryId, webhook.Id, eventName, body, now));
            }

            await webhooks.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not queue the webhook event {Event}.", eventName);
        }
    }
}

/// <summary>
/// Sends the due webhook deliveries (CRM-59): signs the body with the webhook secret, retries failures with
/// <see cref="WebhookBackoff"/>, cancels deliveries of disabled or deleted webhooks. A plain class: the host's worker calls
/// <see cref="RunAsync"/> every minute, tests call it directly.
/// </summary>
public sealed class WebhookDeliveryJob(
    IWebhookRepository webhooks, IWebhookSender sender, ISecretProtector protector, TimeProvider timeProvider, ILogger<WebhookDeliveryJob> logger)
{
    private const int BatchSize = 50;

    /// <summary>Returns how many deliveries were handled.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var due = await webhooks.ListDueAsync(now, BatchSize, cancellationToken);
        foreach (var (delivery, webhook) in due)
        {
            if (webhook is not { IsEnabled: true })
            {
                delivery.Cancel(webhook is null ? IntegrationText.WebhookDeleted : IntegrationText.WebhookDisabled);
                continue;
            }

            await SendAsync(delivery, webhook, now, cancellationToken);
        }

        if (due.Count > 0)
        {
            await webhooks.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    private async Task SendAsync(WebhookDelivery delivery, Webhook webhook, DateTime now, CancellationToken cancellationToken)
    {
        var secret = protector.Unprotect(webhook.ProtectedSecret);
        if (secret is null)
        {
            delivery.RecordFailure(null, "The signing secret cannot be read.", now);
            return;
        }

        var headers = new Dictionary<string, string>
        {
            ["X-Crm-Event"] = delivery.Event,
            ["X-Crm-Delivery"] = delivery.Id.ToString(),
            ["X-Crm-Signature"] = WebhookSignature.Sign(secret, delivery.Payload),
        };
        WebhookSendResult result;
        try
        {
            result = await sender.SendAsync(webhook.Url, delivery.Payload, headers, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = new WebhookSendResult(false, null, exception.Message);
        }

        if (result.Success)
        {
            delivery.RecordSuccess(result.StatusCode ?? 200, now);
            return;
        }

        delivery.RecordFailure(result.StatusCode, result.Error ?? "Delivery failed.", now);
        logger.LogWarning("Webhook {WebhookId} delivery {DeliveryId} failed (attempt {Attempt}, status {Status}): {Error}",
            webhook.Id, delivery.Id, delivery.Attempts, result.StatusCode, result.Error);
    }
}
