using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Crm.Application.Channels;
using Crm.Infrastructure.Channels.WhatsApp;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-25: the Cloud API client and provider (no network: the HTTP handler is a fake).</summary>
public class WhatsAppCloudClientTests
{
    private const string Accepted = """{"messaging_product":"whatsapp","messages":[{"id":"wamid.OUT1"}]}""";

    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public bool Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new HttpRequestException("connection refused");
            }

            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private static WhatsAppChannelOptions Options() => new()
    {
        PhoneNumberId = "PHONE_ID",
        AccessToken = "TOKEN",
        ApiBaseUrl = "https://graph.example.test/v21.0/",
        TemplateLanguage = "ar",
    };

    private static WhatsAppCloudClient Client(FakeHandler handler, WhatsAppChannelOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options());

    [Fact]
    public async Task Send_Text_PostsToTheMessagesEndpoint_WithBearerToken()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Accepted);

        var id = await Client(handler).SendAsync("+966501234567", "Your order ships today.", null, CancellationToken.None);

        Assert.Equal("wamid.OUT1", id);
        Assert.Equal("https://graph.example.test/v21.0/PHONE_ID/messages", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("TOKEN", handler.Request.Headers.Authorization.Parameter);
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("whatsapp", (string?)body["messaging_product"]);
        Assert.Equal("966501234567", (string?)body["to"]);
        Assert.Equal("text", (string?)body["type"]);
        Assert.Equal("Your order ships today.", (string?)body["text"]!["body"]);
    }

    [Fact]
    public async Task Send_Template_UsesTheTemplateAndConfiguredLanguage()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Accepted);

        await Client(handler).SendAsync("+966501234567", "ignored", "order_update", CancellationToken.None);

        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("template", (string?)body["type"]);
        Assert.Equal("order_update", (string?)body["template"]!["name"]);
        Assert.Equal("ar", (string?)body["template"]!["language"]!["code"]);
    }

    [Fact]
    public async Task Send_ErrorStatus_ThrowsWithTheGraphMessage()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, """{"error":{"message":"Recipient phone number not in allowed list","code":131030}}""");

        var error = await Assert.ThrowsAsync<WhatsAppApiException>(
            () => Client(handler).SendAsync("+966501234567", "Hi", null, CancellationToken.None));

        Assert.Equal("Recipient phone number not in allowed list", error.Message);
    }

    [Fact]
    public async Task Provider_NotConfigured_ReturnsFailure_WithoutCallingTheApi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Accepted);
        var options = new WhatsAppChannelOptions();
        var provider = new WhatsAppChannelProvider(options, Client(handler, options));

        var result = await provider.SendAsync(new OutboundChannelMessage(Guid.NewGuid(), "+966501234567", null, "Hi", null), CancellationToken.None);

        Assert.False(provider.IsConfigured);
        Assert.False(result.Succeeded);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Provider_ApiError_AndNetworkError_ReturnFailures()
    {
        var apiError = new FakeHandler(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid OAuth access token."}}""");
        var network = new FakeHandler(HttpStatusCode.OK, Accepted) { Throw = true };
        var message = new OutboundChannelMessage(Guid.NewGuid(), "+966501234567", null, "Hi", null);

        var first = await new WhatsAppChannelProvider(Options(), Client(apiError)).SendAsync(message, CancellationToken.None);
        var second = await new WhatsAppChannelProvider(Options(), Client(network)).SendAsync(message, CancellationToken.None);

        Assert.Equal((false, "Invalid OAuth access token."), (first.Succeeded, first.Error));
        Assert.Equal((false, "connection refused"), (second.Succeeded, second.Error));
    }
}
