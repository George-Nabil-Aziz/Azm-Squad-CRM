using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Application.Channels.Sms;
using Crm.Application.Customers;
using Crm.Domain.Channels;
using Crm.Infrastructure.Channels.Sms;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-57: the SMS API client (stub HTTP handler), the provider webhooks and the segment calculator.</summary>
public class SmsChannelApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string InboundPath = "/api/webhooks/sms";
    private static readonly string SignedBase = CrmApiFactory.SmsWebhookBaseUrl;

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

    private static SmsChannelOptions Options() => new()
    {
        AccountSid = "AC123",
        AuthToken = "secret",
        FromNumber = "+15005550006",
        ApiBaseUrl = "https://sms.example.test/2010-04-01/",
        WebhookBaseUrl = "https://crm.example.com",
    };

    private static SmsChannelProvider Provider(FakeHandler handler, SmsChannelOptions? options = null)
    {
        options ??= Options();
        return new SmsChannelProvider(options, new TwilioSmsClient(new HttpClient(handler), options));
    }

    private static OutboundChannelMessage Message() => new(Guid.NewGuid(), "+966501234567", null, "Your order ships today.", null);

    [Fact]
    public async Task Send_PostsAFormToTheMessagesEndpoint_WithBasicAuth_AndReturnsTheSid()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, """{"sid":"SM777","status":"queued"}""");

        var result = await Provider(handler).SendAsync(Message(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("SM777", result.ProviderMessageId);
        Assert.Equal("https://sms.example.test/2010-04-01/Accounts/AC123/Messages.json", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Basic", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("AC123:secret", Encoding.UTF8.GetString(Convert.FromBase64String(handler.Request.Headers.Authorization.Parameter!)));
        var form = System.Web.HttpUtility.ParseQueryString(handler.Body!);
        Assert.Equal(("+966501234567", "+15005550006", "Your order ships today."), (form["To"], form["From"], form["Body"]));
        Assert.Equal("https://crm.example.com/api/webhooks/sms/status", form["StatusCallback"]);
    }

    [Fact]
    public async Task Send_ProviderError_IsReturnedAsAFailure()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, """{"code":21211,"message":"Invalid 'To' phone number"}""");

        var result = await Provider(handler).SendAsync(Message(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid 'To' phone number", result.Error);
    }

    [Fact]
    public async Task Send_NetworkError_IsReturnedAsAFailure()
    {
        var result = await Provider(new FakeHandler(HttpStatusCode.OK, "{}") { Throw = true }).SendAsync(Message(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("connection refused", result.Error);
    }

    [Fact]
    public async Task Send_WithoutCredentials_FailsAsNotConfigured_WithoutCallingTheApi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");

        var result = await Provider(handler, new SmsChannelOptions()).SendAsync(Message(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(handler.Request);
    }

    // ---- webhooks

    private static string NewNumber() => "+96650" + Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> form, string? token = CrmApiFactory.SmsAuthToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        if (token is not null)
        {
            request.Headers.Add(TwilioSignature.HeaderName, TwilioSignature.Compute(SignedBase + path, form, token));
        }

        return await client.SendAsync(request);
    }

    private static Dictionary<string, string> Incoming(string sid, string from, string body) =>
        new() { ["MessageSid"] = sid, ["From"] = from, ["Body"] = body };

    [Fact]
    public async Task Inbound_WithAnInvalidSignature_Returns401()
    {
        var response = await PostAsync(factory.CreateClient(), InboundPath, Incoming($"SM{Guid.NewGuid():N}", NewNumber(), "Hi"), token: "wrong-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Inbound_WithoutASignature_Returns401()
    {
        var response = await PostAsync(factory.CreateClient(), InboundPath, Incoming($"SM{Guid.NewGuid():N}", NewNumber(), "Hi"), token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_FromAnUnknownNumber_CreatesACustomerAndASmsTicket_ThenTheNextSmsJoinsIt()
    {
        var number = NewNumber();
        var client = factory.CreateClient();
        var firstSid = $"SM{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, InboundPath, Incoming(firstSid, number, "Where is my order?"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, InboundPath, Incoming($"SM{Guid.NewGuid():N}", number, "Hello?"))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Phone == number);
        var ticket = await db.Tickets.AsNoTracking().SingleAsync(t => t.CustomerId == customer.Id);
        Assert.Equal((Crm.Domain.Tickets.TicketChannel.Sms, "Where is my order?"), (ticket.Channel, ticket.Subject));
        Assert.Equal(2, await db.TicketMessages.CountAsync(m => m.TicketId == ticket.Id));
    }

    [Fact]
    public async Task Inbound_FromAKnownNumber_UsesThatCustomer()
    {
        var number = NewNumber();
        Guid customerId;
        using (var scope = factory.Services.CreateScope())
        {
            customerId = (await scope.ServiceProvider.GetRequiredService<ICustomerService>()
                .CreateAsync(new CustomerRequest("Known SMS", null, number), CancellationToken.None)).Id;
        }

        await PostAsync(factory.CreateClient(), InboundPath, Incoming($"SM{Guid.NewGuid():N}", number, "Hi"));

        using var check = factory.Services.CreateScope();
        Assert.Equal(customerId, (await check.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.AsNoTracking().SingleAsync(t => t.CustomerId == customerId)).CustomerId);
    }

    [Fact]
    public async Task Inbound_TheSameMessageSidTwice_IsStoredOnce()
    {
        var number = NewNumber();
        var form = Incoming($"SM{Guid.NewGuid():N}", number, "Once");
        var client = factory.CreateClient();

        await PostAsync(client, InboundPath, form);
        await PostAsync(client, InboundPath, form);

        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CrmDbContext>().ReceivedMessages.CountAsync(m => m.From == number));
    }

    [Fact]
    public async Task Status_Failed_MarksTheOutboundMessageFailed()
    {
        var sid = $"SM{Guid.NewGuid():N}";
        Guid messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var message = OutboundMessage.Create(ChannelKind.Sms, "+966501234567", null, "Hi", null, null, DateTime.UtcNow);
            message.MarkSent(sid, DateTime.UtcNow);
            db.OutboundMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        var response = await PostAsync(factory.CreateClient(), InboundPath + "/status",
            new Dictionary<string, string> { ["MessageSid"] = sid, ["MessageStatus"] = "undelivered", ["ErrorCode"] = "30003" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<CrmDbContext>().OutboundMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);
        Assert.Equal(DeliveryStatus.Failed, stored.Status);
    }

    // ---- segments (AC 4)

    [Fact]
    public async Task Segments_ReportsTheCountForAgents_AndNeedsAPermission()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/channels/sms/segments", new { text = "Hi" })).StatusCode);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var info = await (await agent.PostAsJsonAsync("/api/channels/sms/segments", new { text = new string('a', 161) })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((2, "gsm7"), (info.GetProperty("segments").GetInt32(), info.GetProperty("encoding").GetString()));
    }

    [Fact]
    public async Task ChannelStatus_ReportsSmsAsNotConfigured_WithoutCredentials()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var status = await admin.GetFromJsonAsync<JsonElement>("/api/channels/status");

        Assert.False(status.GetProperty("sms").GetProperty("configured").GetBoolean());
    }
}
