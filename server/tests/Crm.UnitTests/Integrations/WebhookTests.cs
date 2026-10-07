using Crm.Application.Common.Exceptions;
using Crm.Application.Integrations;
using Crm.Application.Settings;
using Crm.Domain.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.UnitTests.Integrations;

public class WebhookSignatureTests
{
    [Fact]
    public void Sign_IsTheHmacSha256OfTheBody_AndVerifiable()
    {
        var signature = WebhookSignature.Sign("whsec_x", "{\"a\":1}");

        Assert.StartsWith("sha256=", signature);
        Assert.Equal(7 + 64, signature.Length);
        Assert.True(WebhookSignature.Verify("whsec_x", "{\"a\":1}", signature));
        Assert.False(WebhookSignature.Verify("whsec_y", "{\"a\":1}", signature));
        Assert.False(WebhookSignature.Verify("whsec_x", "{\"a\":2}", signature));
        Assert.False(WebhookSignature.Verify("whsec_x", "{\"a\":1}", null));
    }

    [Fact]
    public void Sign_MatchesAKnownVector() =>
        // HMAC_SHA256(key "key", "The quick brown fox jumps over the lazy dog") from RFC 4231 style references.
        Assert.Equal("sha256=f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8",
            WebhookSignature.Sign("key", "The quick brown fox jumps over the lazy dog"));
}

public class WebhookBackoffTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 120)]
    [InlineData(5, 360)]
    public void NextAttempt_FollowsTheBackoffSchedule(int failures, int minutes) =>
        Assert.Equal(Now.AddMinutes(minutes), WebhookBackoff.NextAttemptAt(failures, Now));

    [Fact]
    public void AfterTheLastRetry_NoTryIsLeft()
    {
        Assert.Null(WebhookBackoff.NextAttemptAt(6, Now));
        Assert.Equal(6, WebhookBackoff.MaxAttempts);
    }

    [Fact]
    public void Delivery_FailsOverTimeAndIsGivenUp()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), WebhookEvents.TicketCreated, "{}", Now);

        for (var i = 1; i <= 5; i++)
        {
            delivery.RecordFailure(500, "boom", Now);
            Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        }

        delivery.RecordFailure(500, "boom", Now);
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(6, delivery.Attempts);
    }
}

public class WebhookServiceTests
{
    private readonly FakeWebhookRepository _repo = new();
    private readonly IntegrationClock _time = new(new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero));

    private WebhookService Service() => new(_repo, new ReverseProtector(), _time, new WebhookRequestValidator());

    private static WebhookRequest Valid(params string[] events) =>
        new("CRM sync", "https://example.test/hook", events.Length == 0 ? [WebhookEvents.TicketCreated] : events);

    [Fact]
    public async Task Create_ShowsTheSecretOnce_AndStoresItProtected()
    {
        var created = await Service().CreateAsync(Valid(WebhookEvents.TicketCreated, WebhookEvents.TicketResolved), default);

        var stored = Assert.Single(_repo.Webhooks);
        Assert.StartsWith("whsec_", created.Secret);
        Assert.NotEqual(created.Secret, stored.ProtectedSecret);
        Assert.Equal(created.Secret, new ReverseProtector().Unprotect(stored.ProtectedSecret));
        Assert.Equal(["ticket.created", "ticket.resolved"], created.Events);
        Assert.True(created.IsEnabled);
    }

    [Theory]
    [InlineData("", "https://x.test", "ticket.created", "name")]
    [InlineData("n", "ftp://x.test", "ticket.created", "url")]
    [InlineData("n", "not a url", "ticket.created", "url")]
    [InlineData("n", "https://x.test", "ticket.deleted", "events[0]")]
    public async Task Create_RejectsBadInput(string name, string url, string eventName, string field)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new WebhookRequest(name, url, [eventName]), default));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Empty(_repo.Webhooks);
    }

    [Fact]
    public async Task Create_WithoutEvents_IsRejected() =>
        Assert.Contains("events", (await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new WebhookRequest("n", "https://x.test", []), default))).Errors.Keys);

    [Fact]
    public async Task Disable_CancelsPendingDeliveries_AndEnableKeepsTheWebhook()
    {
        var created = await Service().CreateAsync(Valid(), default);
        var pending = WebhookDelivery.Create(Guid.NewGuid(), created.Id, WebhookEvents.TicketCreated, "{}", _time.GetUtcNow().UtcDateTime);
        _repo.Deliveries.Add(pending);

        var disabled = await Service().SetEnabledAsync(created.Id, false, default);

        Assert.False(disabled.IsEnabled);
        Assert.Equal(WebhookDeliveryStatus.Failed, pending.Status);
        Assert.True((await Service().SetEnabledAsync(created.Id, true, default)).IsEnabled);
    }

    [Fact]
    public async Task UnknownWebhook_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service().SetEnabledAsync(Guid.NewGuid(), false, default));
}

public class WebhookPublisherAndJobTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeWebhookRepository _repo = new();
    private readonly IntegrationClock _time = new(Start);
    private readonly FakeWebhookSender _sender = new();

    private Webhook AddWebhook(string events = "ticket.created", bool enabled = true)
    {
        var webhook = Webhook.Create("w", "https://example.test/hook", events.Split(','), new ReverseProtector().Protect("whsec_secret"), Start.UtcDateTime);
        webhook.SetEnabled(enabled, Start.UtcDateTime);
        _repo.Webhooks.Add(webhook);
        return webhook;
    }

    private WebhookEventPublisher Publisher() => new(_repo, _time, NullLogger<WebhookEventPublisher>.Instance);

    private WebhookDeliveryJob Job() => new(_repo, _sender, new ReverseProtector(), _time, NullLogger<WebhookDeliveryJob>.Instance);

    [Fact]
    public async Task Publish_QueuesOneDeliveryPerEnabledSubscribedWebhook()
    {
        var subscribed = AddWebhook("ticket.created,ticket.resolved");
        AddWebhook("ticket.resolved");
        AddWebhook("ticket.created", enabled: false);

        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { number = "TKT-000001" }, default);

        var delivery = Assert.Single(_repo.Deliveries);
        Assert.Equal(subscribed.Id, delivery.WebhookId);
        Assert.Contains("\"event\":\"ticket.created\"", delivery.Payload);
        Assert.Contains("\"number\":\"TKT-000001\"", delivery.Payload);
        Assert.Contains($"\"id\":\"{delivery.Id}\"", delivery.Payload);
    }

    [Fact]
    public async Task Publish_NeverThrows_WhenStorageFails()
    {
        AddWebhook();
        _repo.FailOnSave = true;

        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { }, default);
    }

    [Fact]
    public async Task Job_SendsASignedDelivery_AndMarksItDelivered()
    {
        var webhook = AddWebhook();
        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { a = 1 }, default);

        var handled = await Job().RunAsync(default);

        Assert.Equal(1, handled);
        var call = Assert.Single(_sender.Calls);
        Assert.Equal(webhook.Url, call.Url);
        Assert.True(WebhookSignature.Verify("whsec_secret", call.Body, call.Headers["X-Crm-Signature"]));
        Assert.Equal("ticket.created", call.Headers["X-Crm-Event"]);
        var delivery = _repo.Deliveries.Single();
        Assert.Equal(delivery.Id.ToString(), call.Headers["X-Crm-Delivery"]);
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(0, await Job().RunAsync(default)); // nothing pending any more
    }

    [Fact]
    public async Task Job_RetriesFailuresWithBackoff_ThenGivesUp()
    {
        AddWebhook();
        _sender.Result = new WebhookSendResult(false, 500, "The receiver answered 500");
        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { }, default);
        var delivery = _repo.Deliveries.Single();

        await Job().RunAsync(default);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(Start.UtcDateTime.AddMinutes(1), delivery.NextAttemptAt);
        Assert.Equal(500, delivery.LastStatusCode);
        Assert.Equal("The receiver answered 500", delivery.LastError);

        Assert.Equal(0, await Job().RunAsync(default)); // not due yet
        _time.UtcNow = Start.AddMinutes(1);
        await Job().RunAsync(default);
        Assert.Equal(Start.UtcDateTime.AddMinutes(1 + 5), delivery.NextAttemptAt);

        foreach (var delay in new[] { 5, 30, 120, 360 })
        {
            _time.UtcNow = new DateTimeOffset(delivery.NextAttemptAt, TimeSpan.Zero);
            await Job().RunAsync(default);
        }

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(6, delivery.Attempts);
        Assert.Equal(6, _sender.Calls.Count);
    }

    [Fact]
    public async Task Job_AnExceptionFromTheSender_CountsAsAFailure()
    {
        AddWebhook();
        _sender.Throw = true;
        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { }, default);

        await Job().RunAsync(default);

        var delivery = _repo.Deliveries.Single();
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal("network down", delivery.LastError);
    }

    [Fact]
    public async Task Job_DoesNotSendToADisabledWebhook()
    {
        var webhook = AddWebhook();
        await Publisher().PublishAsync(WebhookEvents.TicketCreated, new { }, default);
        webhook.SetEnabled(false, Start.UtcDateTime); // disabled after the event was queued, before it was sent

        await Job().RunAsync(default);

        Assert.Empty(_sender.Calls);
        Assert.Equal(WebhookDeliveryStatus.Failed, _repo.Deliveries.Single().Status);
    }
}

internal sealed class ReverseProtector : ISecretProtector
{
    public string Protect(string plainText) => "enc:" + new string([.. plainText.Reverse()]);

    public string? Unprotect(string protectedText) =>
        protectedText.StartsWith("enc:", StringComparison.Ordinal) ? new string([.. protectedText[4..].Reverse()]) : null;
}

internal sealed class FakeWebhookSender : IWebhookSender
{
    public List<(string Url, string Body, IReadOnlyDictionary<string, string> Headers)> Calls { get; } = [];

    public WebhookSendResult Result { get; set; } = new(true, 200, null);

    public bool Throw { get; set; }

    public Task<WebhookSendResult> SendAsync(string url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        Calls.Add((url, body, headers));
        return Throw ? throw new InvalidOperationException("network down") : Task.FromResult(Result);
    }
}

internal sealed class FakeWebhookRepository : IWebhookRepository
{
    public List<Webhook> Webhooks { get; } = [];

    public List<WebhookDelivery> Deliveries { get; } = [];

    public bool FailOnSave { get; set; }

    public void Add(Webhook webhook) => Webhooks.Add(webhook);

    public void Remove(Webhook webhook) => Webhooks.Remove(webhook);

    public void AddDelivery(WebhookDelivery delivery) => Deliveries.Add(delivery);

    public Task<Webhook?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Webhooks.FirstOrDefault(w => w.Id == id));

    public Task<IReadOnlyList<Webhook>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Webhook>>([.. Webhooks]);

    public Task<IReadOnlyList<Webhook>> ListEnabledForEventAsync(string eventName, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Webhook>>([.. Webhooks.Where(w => w.IsEnabled && w.IsSubscribedTo(eventName))]);

    public Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(Guid webhookId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebhookDelivery>>([.. Deliveries.Where(d => d.WebhookId == webhookId).Take(take)]);

    public Task<IReadOnlyList<WebhookDeliveryWork>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebhookDeliveryWork>>([.. Deliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= utcNow).Take(take)
            .Select(d => new WebhookDeliveryWork(d, Webhooks.FirstOrDefault(w => w.Id == d.WebhookId)))]);

    public Task<IReadOnlyList<WebhookDelivery>> ListPendingAsync(Guid webhookId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebhookDelivery>>([.. Deliveries.Where(d => d.WebhookId == webhookId && d.Status == WebhookDeliveryStatus.Pending)]);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        FailOnSave ? throw new InvalidOperationException("db down") : Task.CompletedTask;
}
