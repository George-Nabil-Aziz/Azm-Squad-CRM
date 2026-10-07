using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Integrations;
using Crm.Infrastructure.Integrations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Integrations;

/// <summary>CRM-60: ERP link, read-only ERP data, the sync log and the HTTP client, with the ERP faked.</summary>
public class ErpApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record ErpBody(bool Linked, string? ErpCustomerId, bool Available, string? Message, OrderBody[] Orders, InvoiceBody[] Invoices);

    private sealed record OrderBody(string Id, string? Number, string? Status, decimal? Total);

    private sealed record InvoiceBody(string Id, string? Number);

    private sealed record LinkBody(Guid CustomerId, string? ErpCustomerId);

    private sealed record LogPage(LogBody[] Items, int TotalCount);

    private sealed record LogBody(Guid CustomerId, string? CustomerName, string ErpCustomerId, string Result, string? Error, DateTime CreatedAt);

    private sealed record CustomerBody(Guid Id, string? ErpCustomerId);

    private sealed class FakeClient : IErpClient
    {
        public ErpCustomerData Data { get; set; } = new(
            [new ErpOrder("o1", "SO-1", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), "shipped", 120m, "SAR")],
            [new ErpInvoice("i1", "INV-1", new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), null, "paid", 120m, "SAR")]);

        public Exception? Failure { get; set; }

        public Task<ErpCustomerData> GetCustomerDataAsync(string erpCustomerId, int limit, CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult(Data) : Task.FromException<ErpCustomerData>(Failure);
    }

    private WebApplicationFactory<Program> WithClient(FakeClient client) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IErpClient>();
            services.AddSingleton<IErpClient>(client);
        }));

    private async Task<HttpClient> SignedIn(WebApplicationFactory<Program> host, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, role);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
        return client;
    }

    private static string ErpId() => $"ERP-{Guid.NewGuid():N}";

    [Fact]
    public async Task LinkingACustomer_ShowsTheErpIdOnTheCustomer_AndAnotherCustomerCannotTakeIt()
    {
        await using var host = WithClient(new FakeClient());
        var agent = await SignedIn(host, Roles.Agent);
        var first = await TicketArrange.CustomerAsync(agent, "Linked Co");
        var second = await TicketArrange.CustomerAsync(agent, "Other Co");
        var erpId = ErpId();

        var link = await agent.PutAsJsonAsync($"/api/customers/{first}/erp-link", new { erpCustomerId = erpId });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        Assert.Equal(erpId, (await link.Content.ReadFromJsonAsync<LinkBody>())!.ErpCustomerId);
        Assert.Equal(erpId, (await agent.GetFromJsonAsync<CustomerBody>($"/api/customers/{first}"))!.ErpCustomerId);

        Assert.Equal(HttpStatusCode.Conflict, (await agent.PutAsJsonAsync($"/api/customers/{second}/erp-link", new { erpCustomerId = erpId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PutAsJsonAsync($"/api/customers/{first}/erp-link", new { erpCustomerId = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PutAsJsonAsync($"/api/customers/{second}/erp-link", new { erpCustomerId = erpId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PutAsJsonAsync($"/api/customers/{first}/erp-link", new { erpCustomerId = new string('x', 101) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}/erp-link", new { erpCustomerId = "x" })).StatusCode);
    }

    [Fact]
    public async Task TheCustomerPanel_ShowsRecentOrdersAndInvoices_ReadOnly()
    {
        await using var host = WithClient(new FakeClient());
        var agent = await SignedIn(host, Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var unlinked = (await agent.GetFromJsonAsync<ErpBody>($"/api/customers/{customerId}/erp"))!;
        Assert.False(unlinked.Linked);

        await agent.PutAsJsonAsync($"/api/customers/{customerId}/erp-link", new { erpCustomerId = ErpId() });
        var data = (await agent.GetFromJsonAsync<ErpBody>($"/api/customers/{customerId}/erp"))!;

        Assert.True(data.Linked);
        Assert.True(data.Available);
        Assert.Equal("SO-1", Assert.Single(data.Orders).Number);
        Assert.Equal("INV-1", Assert.Single(data.Invoices).Number);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/customers/{Guid.NewGuid()}/erp")).StatusCode);
    }

    [Fact]
    public async Task WhenTheErpIsDown_ThePageStillLoads_WithAMessage()
    {
        var erp = new FakeClient { Failure = new ErpUnavailableException("The ERP answered 503") };
        await using var host = WithClient(erp);
        var agent = await SignedIn(host, Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        await agent.PutAsJsonAsync($"/api/customers/{customerId}/erp-link", new { erpCustomerId = ErpId() });

        var response = await agent.GetAsync($"/api/customers/{customerId}/erp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ErpBody>())!;
        Assert.True(body.Linked);
        Assert.False(body.Available);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
        Assert.Empty(body.Orders);
        // The customer itself still loads.
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/customers/{customerId}")).StatusCode);
    }

    [Fact]
    public async Task EverySync_IsLogged_WithResultAndTime()
    {
        var erp = new FakeClient();
        await using var host = WithClient(erp);
        var agent = await SignedIn(host, Roles.Agent);
        var admin = await SignedIn(host, Roles.Admin);
        var customerId = await TicketArrange.CustomerAsync(agent, "Logged Co");
        var erpId = ErpId();
        await agent.PutAsJsonAsync($"/api/customers/{customerId}/erp-link", new { erpCustomerId = erpId });
        var before = DateTime.UtcNow.AddMinutes(-1);

        await agent.GetAsync($"/api/customers/{customerId}/erp");
        erp.Failure = new ErpUnavailableException("The ERP answered 503");
        await agent.GetAsync($"/api/customers/{customerId}/erp");
        erp.Failure = new ErpNotConfiguredException();
        await agent.GetAsync($"/api/customers/{customerId}/erp");

        var logs = (await admin.GetFromJsonAsync<LogPage>("/api/integrations/erp/sync-logs?pageSize=100"))!.Items.Where(l => l.ErpCustomerId == erpId).ToList();
        Assert.Equal(["failed", "not_configured", "success"], logs.Select(l => l.Result).Order());
        Assert.All(logs, l =>
        {
            Assert.Equal(customerId, l.CustomerId);
            Assert.Equal("Logged Co", l.CustomerName);
            Assert.True(l.CreatedAt >= before);
        });
        Assert.Equal("The ERP answered 503", logs.Single(l => l.Result == "failed").Error);
    }

    [Fact]
    public async Task Authorization_IsEnforcedOnTheApi()
    {
        await using var host = WithClient(new FakeClient());
        var agent = await SignedIn(host, Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/integrations/erp/sync-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/api/integrations/erp/sync-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync($"/api/customers/{customerId}/erp")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().PutAsJsonAsync($"/api/customers/{customerId}/erp-link", new { erpCustomerId = "x" })).StatusCode);
    }
}

/// <summary>The real HTTP client against a stubbed ERP (no network).</summary>
public class HttpErpClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static HttpErpClient Client(StubHandler handler, params (string, string?)[] settings) => new(
        new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Item1, s => s.Item2)).Build());

    [Fact]
    public async Task ReadsOrdersAndInvoices_WithTheBearerToken()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/orders", StringComparison.Ordinal)
            ? Json("""[{"id":"o1","number":"SO-1","date":"2026-09-01T00:00:00Z","status":"shipped","total":120.5,"currency":"SAR"}]""")
            : Json("""[{"id":"i1","number":"INV-1","date":"2026-09-02T00:00:00Z","dueDate":"2026-10-02T00:00:00Z","status":"paid","total":120.5,"currency":"SAR"}]"""));
        var client = Client(handler, ("Integrations:Erp:BaseUrl", "https://erp.test/api/"), ("Integrations:Erp:ApiKey", "secret-key"));

        var data = await client.GetCustomerDataAsync("A/1 b", 5, default);

        Assert.Equal("SO-1", Assert.Single(data.Orders).Number);
        Assert.Equal(120.5m, data.Orders[0].Total);
        Assert.Equal("INV-1", Assert.Single(data.Invoices).Number);
        Assert.Equal("https://erp.test/api/customers/A%2F1%20b/orders?limit=5", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer secret-key", handler.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task WithoutABaseUrl_ItIsNotConfigured_AndNothingIsCalled()
    {
        var handler = new StubHandler(_ => Json("[]"));

        await Assert.ThrowsAsync<ErpNotConfiguredException>(() => Client(handler).GetCustomerDataAsync("1", 5, default));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task AnErrorOrUnreadableAnswer_IsUnavailable(HttpStatusCode status, string body)
    {
        var client = Client(new StubHandler(_ => Json(body, status)), ("Integrations:Erp:BaseUrl", "https://erp.test"));

        await Assert.ThrowsAsync<ErpUnavailableException>(() => client.GetCustomerDataAsync("1", 5, default));
    }

    [Fact]
    public async Task ANetworkError_IsUnavailable()
    {
        var client = Client(new StubHandler(_ => throw new HttpRequestException("connection refused")), ("Integrations:Erp:BaseUrl", "https://erp.test"));

        var error = await Assert.ThrowsAsync<ErpUnavailableException>(() => client.GetCustomerDataAsync("1", 5, default));

        Assert.Contains("connection refused", error.Message);
    }
}
