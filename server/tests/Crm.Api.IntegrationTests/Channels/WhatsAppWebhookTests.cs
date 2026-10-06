using System.Net;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Channels.WhatsApp;
using Crm.Application.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-26 (Phase 1): the WhatsApp Cloud API webhook.</summary>
public class WhatsAppWebhookTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Path = "/api/webhooks/whatsapp";

    internal static byte[] TextPayload(string messageId, string waId, string? name, string text) =>
        Encoding.UTF8.GetBytes($$$"""
            {"object":"whatsapp_business_account","entry":[{"id":"WABA","changes":[{"field":"messages","value":{
              "messaging_product":"whatsapp","metadata":{"phone_number_id":"PHONE_ID"},
              "contacts":[{ {{{(name is null ? "" : $"\"profile\":{{\"name\":\"{name}\"}},")}}} "wa_id":"{{{waId}}}"}],
              "messages":[{"from":"{{{waId}}}","id":"{{{messageId}}}","timestamp":"1791273600","type":"text","text":{"body":"{{{text}}}"}}]}}]}]}
            """);

    internal static async Task<HttpResponseMessage> PostSignedAsync(HttpClient client, byte[] body, string? secret = CrmApiFactory.WhatsAppAppSecret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        if (secret is not null)
        {
            request.Headers.Add(WhatsAppSignature.HeaderName, WhatsAppSignature.Compute(body, secret));
        }

        return await client.SendAsync(request);
    }

    /// <summary>A random Saudi mobile number as WhatsApp sends it (digits, no "+").</summary>
    private static string NewWaId() => "9665" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Verify_WithTheRightToken_ReturnsTheChallenge()
    {
        var response = await factory.CreateClient().GetAsync(
            $"{Path}?hub.mode=subscribe&hub.verify_token={CrmApiFactory.WhatsAppVerifyToken}&hub.challenge=1158201444");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1158201444", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Verify_WithAWrongToken_Returns403()
    {
        var response = await factory.CreateClient().GetAsync($"{Path}?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithAnInvalidSignature_Returns401()
    {
        var body = TextPayload("wamid.bad", NewWaId(), "Nour", "Hi");

        var response = await PostSignedAsync(factory.CreateClient(), body, secret: "not-the-app-secret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WithoutSignature_Returns401()
    {
        var response = await PostSignedAsync(factory.CreateClient(), TextPayload("wamid.none", NewWaId(), "Nour", "Hi"), secret: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_FromAKnownNumber_LinksTheMessageToThatCustomer()
    {
        var waId = NewWaId();
        Guid customerId;
        using (var scope = factory.Services.CreateScope())
        {
            customerId = (await scope.ServiceProvider.GetRequiredService<ICustomerService>()
                .CreateAsync(new CustomerRequest("Known WhatsApp", null, "+" + waId), CancellationToken.None)).Id;
        }

        var messageId = $"wamid.{Guid.NewGuid():N}";
        var response = await PostSignedAsync(factory.CreateClient(), TextPayload(messageId, waId, "Someone", "Where is my order?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<CrmDbContext>().ReceivedMessages.AsNoTracking()
            .SingleAsync(m => m.ExternalId == messageId);
        Assert.Equal((customerId, "+" + waId, "Where is my order?"), (stored.CustomerId!.Value, stored.From, stored.Body));
    }

    [Fact]
    public async Task Post_FromAnUnknownNumber_CreatesACustomer()
    {
        var waId = NewWaId();
        var messageId = $"wamid.{Guid.NewGuid():N}";

        var response = await PostSignedAsync(factory.CreateClient(), TextPayload(messageId, waId, "Nour Ali", "Hello"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().ReceivedMessages.AsNoTracking()
            .SingleAsync(m => m.ExternalId == messageId);
        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerService>().GetAsync(stored.CustomerId!.Value, CancellationToken.None);
        Assert.Equal(("Nour Ali", "+" + waId), (customer.Name, customer.Phone));
        Assert.Contains(customer.Contacts, c => c is { Type: "whatsapp", IsPrimary: true } && c.Value == "+" + waId);
    }

    [Fact]
    public async Task Post_SameMessageTwice_StoresItOnce()
    {
        var waId = NewWaId();
        var body = TextPayload($"wamid.{Guid.NewGuid():N}", waId, "Twice", "Hello");
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await PostSignedAsync(client, body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostSignedAsync(client, body)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(1, await db.ReceivedMessages.CountAsync(m => m.From == "+" + waId));
        Assert.Equal(1, await db.Customers.CountAsync(c => c.Phone == "+" + waId));
    }

    [Fact]
    public async Task Post_UnknownPayload_WithAValidSignature_Returns200()
    {
        var response = await PostSignedAsync(factory.CreateClient(), Encoding.UTF8.GetBytes("{\"object\":\"page\"}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
