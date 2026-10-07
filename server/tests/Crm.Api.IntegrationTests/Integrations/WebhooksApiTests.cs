using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Integrations;
using Crm.Domain.Integrations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Integrations;

/// <summary>CRM-59: outgoing webhooks through the real host and database; the HTTP call to the receiver is faked.</summary>
public class WebhooksApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record CreatedWebhook(Guid Id, string Name, string Url, string[] Events, bool IsEnabled, string Secret);

    private sealed record ListedWebhook(Guid Id, bool IsEnabled);

    private sealed record DeliveryBody(Guid Id, string Event, string Status, int Attempts, string? LastError, int? LastStatusCode);

    private sealed class RecordingSender : IWebhookSender
    {
        public List<(string Url, string Body, IReadOnlyDictionary<string, string> Headers)> Calls { get; } = [];

        public WebhookSendResult Result { get; set; } = new(true, 200, null);

        public Task<WebhookSendResult> SendAsync(string url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add((url, body, headers));
            }

            return Task.FromResult(Result);
        }
    }

    private WebApplicationFactory<Program> WithSender(RecordingSender sender) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWebhookSender>();
            services.AddSingleton<IWebhookSender>(sender);
        }));

    private static async Task<CreatedWebhook> RegisterAsync(HttpClient admin, string url, params string[] events)
    {
        var response = await admin.PostAsJsonAsync("/api/webhooks", new { name = $"Hook {Guid.NewGuid():N}", url, events });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedWebhook>())!;
    }

    private static async Task<DeliveryBody[]> DeliveriesAsync(HttpClient admin, Guid webhookId) =>
        (await admin.GetFromJsonAsync<DeliveryBody[]>($"/api/webhooks/{webhookId}/deliveries"))!;

    private static async Task RunJobAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WebhookDeliveryJob>().RunAsync(default);
    }

    private static async Task ResolveAsync(HttpClient client, Guid ticketId)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status = "open" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status = "resolved" })).StatusCode);
    }

    [Fact]
    public async Task Register_ShowsTheSecretOnce_AndValidates()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var created = await RegisterAsync(admin, "https://example.test/hook", "ticket.created", "ticket.resolved");

        Assert.StartsWith("whsec_", created.Secret);
        Assert.True(created.IsEnabled);
        var raw = await (await admin.GetAsync("/api/webhooks")).Content.ReadAsStringAsync();
        Assert.Contains(created.Id.ToString(), raw);
        Assert.DoesNotContain(created.Secret, raw);

        var bad = await admin.PostAsJsonAsync("/api/webhooks", new { name = "x", url = "ftp://nope", events = new[] { "ticket.deleted" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = await bad.Content.ReadAsStringAsync();
        Assert.Contains("url", body);
        Assert.Contains("events", body);
    }

    [Fact]
    public async Task AdminEndpoints_AreForAdminsOnly()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var payload = new { name = "x", url = "https://example.test", events = new[] { "ticket.created" } };

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/webhooks", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync("/api/webhooks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/webhooks")).StatusCode);
    }

    [Fact]
    public async Task CreatingAndResolvingATicket_QueuesDeliveriesForSubscribedWebhooksOnly()
    {
        var sender = new RecordingSender();
        await using var host = WithSender(sender);
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync());
        var both = await RegisterAsync(admin, "https://one.test/hook", "ticket.created", "ticket.resolved");
        var resolvedOnly = await RegisterAsync(admin, "https://two.test/hook", "ticket.resolved");

        var customerId = await TicketArrange.CustomerAsync(admin);
        var ticket = await TicketArrange.TicketAsync(admin, customerId, "Webhook me");

        Assert.Equal(["ticket.created"], (await DeliveriesAsync(admin, both.Id)).Select(d => d.Event));
        Assert.Empty(await DeliveriesAsync(admin, resolvedOnly.Id));

        await ResolveAsync(admin, ticket.Id);

        Assert.Equal(["ticket.created", "ticket.resolved"], (await DeliveriesAsync(admin, both.Id)).Select(d => d.Event).Order());
        Assert.Equal(["ticket.resolved"], (await DeliveriesAsync(admin, resolvedOnly.Id)).Select(d => d.Event));

        await RunJobAsync(host);

        var oneCalls = sender.Calls.Where(c => c.Url == "https://one.test/hook").ToList();
        Assert.Equal(2, oneCalls.Count);
        var created = oneCalls.Single(c => c.Headers["X-Crm-Event"] == "ticket.created");
        Assert.True(WebhookSignature.Verify(both.Secret, created.Body, created.Headers["X-Crm-Signature"]));
        using var json = JsonDocument.Parse(created.Body);
        Assert.Equal("ticket.created", json.RootElement.GetProperty("event").GetString());
        Assert.Equal(ticket.Id, json.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        Assert.All(await DeliveriesAsync(admin, both.Id), d => Assert.Equal("delivered", d.Status));
    }

    [Fact]
    public async Task FailedDeliveries_AreRetriedWithBackoff_AndLogged()
    {
        var sender = new RecordingSender { Result = new WebhookSendResult(false, 503, "The receiver answered 503 Service Unavailable") };
        await using var host = WithSender(sender);
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync());
        var hook = await RegisterAsync(admin, "https://down.test/hook", "ticket.created");
        await TicketArrange.TicketAsync(admin, await TicketArrange.CustomerAsync(admin));

        await RunJobAsync(host);
        var afterFirst = Assert.Single(await DeliveriesAsync(admin, hook.Id));
        Assert.Equal("pending", afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.Equal(503, afterFirst.LastStatusCode);
        Assert.Contains("503", afterFirst.LastError);

        await RunJobAsync(host); // the next try is due in a minute: nothing happens yet
        Assert.Equal(1, (await DeliveriesAsync(admin, hook.Id)).Single().Attempts);

        factory.Time.Advance(TimeSpan.FromMinutes(2));
        sender.Result = new WebhookSendResult(true, 200, null); // the receiver is back
        await RunJobAsync(host);

        var delivered = Assert.Single(await DeliveriesAsync(admin, hook.Id));
        Assert.Equal("delivered", delivered.Status);
        Assert.Equal(2, delivered.Attempts);
    }

    [Fact]
    public async Task DisablingAWebhook_StopsAllDeliveries()
    {
        var sender = new RecordingSender();
        await using var host = WithSender(sender);
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync());
        var hook = await RegisterAsync(admin, "https://off.test/hook", "ticket.created");
        var customerId = await TicketArrange.CustomerAsync(admin);
        await TicketArrange.TicketAsync(admin, customerId, "Queued before disabling");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/webhooks/{hook.Id}/disable", null)).StatusCode);
        await TicketArrange.TicketAsync(admin, customerId, "Created after disabling");
        await RunJobAsync(host);

        Assert.DoesNotContain(sender.Calls, c => c.Url == "https://off.test/hook");
        var deliveries = await DeliveriesAsync(admin, hook.Id);
        Assert.Single(deliveries); // the second ticket queued nothing
        Assert.Equal("failed", deliveries[0].Status); // the pending one was cancelled, not sent
        Assert.Contains("disabled", deliveries[0].LastError);
        Assert.False((await admin.GetFromJsonAsync<ListedWebhook[]>("/api/webhooks"))!.Single(w => w.Id == hook.Id).IsEnabled);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/webhooks/{hook.Id}/enable", null)).StatusCode);
        await TicketArrange.TicketAsync(admin, customerId, "Created after enabling");
        Assert.Equal(2, (await DeliveriesAsync(admin, hook.Id)).Length);
    }

    [Fact]
    public async Task UpdateAndDelete_Work_AndUnknownIdsAre404()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var hook = await RegisterAsync(admin, "https://old.test/hook", "ticket.created");

        var update = await admin.PutAsJsonAsync($"/api/webhooks/{hook.Id}",
            new { name = "Renamed", url = "https://new.test/hook", events = new[] { "ticket.resolved" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Contains("https://new.test/hook", await update.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/webhooks/{hook.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/webhooks/{hook.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/webhooks/{Guid.NewGuid()}/deliveries")).StatusCode);
    }
}
