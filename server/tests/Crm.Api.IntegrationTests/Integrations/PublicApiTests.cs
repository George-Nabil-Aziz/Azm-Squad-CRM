using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.IntegrationTests.Integrations;

/// <summary>CRM-58: API keys (admin side) and the public /api/v1 API through the real host and database.</summary>
public class PublicApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record CreatedKey(Guid Id, string Name, string KeyPrefix, string[] Scopes, string Key);

    private sealed record ListedKey(Guid Id, string KeyPrefix, string[] Scopes, DateTime? RevokedAt);

    private sealed record PageBody(JsonItem[] Items, int TotalCount);

    private sealed record JsonItem(Guid Id);

    internal static async Task<CreatedKey> CreateKeyAsync(HttpClient admin, params string[] scopes)
    {
        var response = await admin.PostAsJsonAsync("/api/api-keys", new { name = $"Key {Guid.NewGuid():N}", scopes });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedKey>())!;
    }

    internal static HttpClient WithKey(HttpClient client, string? key)
    {
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        }

        return client;
    }

    [Fact]
    public async Task Create_ShowsTheKeyOnce_AndStoresOnlyAHash()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var created = await CreateKeyAsync(admin, "tickets:read");

        Assert.StartsWith("crm_", created.Key);
        var listed = (await admin.GetFromJsonAsync<ListedKey[]>("/api/api-keys"))!.Single(k => k.Id == created.Id);
        Assert.Equal(created.KeyPrefix, listed.KeyPrefix);
        var rawList = await (await admin.GetAsync("/api/api-keys")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(created.Key, rawList);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().ApiKeys.AsNoTracking().SingleAsync(k => k.Id == created.Id);
        Assert.NotEqual(created.Key, stored.KeyHash);
        Assert.Equal(64, stored.KeyHash.Length);
    }

    [Fact]
    public async Task AdminEndpoints_ValidateAndAuthorize()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var bad = await admin.PostAsJsonAsync("/api/api-keys", new { name = "", scopes = new[] { "bogus" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/api-keys", new { name = "x", scopes = new[] { "tickets:read" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/api-keys")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/api-keys")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/api-keys/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ReadScope_CanReadTickets_ButCreatingOneIsForbidden()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var customerId = await TicketArrange.CustomerAsync(admin);
        var ticket = await TicketArrange.TicketAsync(admin, customerId);
        var key = await CreateKeyAsync(admin, "tickets:read");
        var api = WithKey(factory.CreateClient(), key.Key);

        var list = await api.GetFromJsonAsync<PageBody>("/api/v1/tickets");
        Assert.Contains(list!.Items, t => t.Id == ticket.Id);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync($"/api/v1/tickets/{ticket.Id}")).StatusCode);

        var create = await api.PostAsJsonAsync("/api/v1/tickets", new { customerId, subject = "From outside" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/v1/customers")).StatusCode);
    }

    [Fact]
    public async Task WriteScopes_CreateTicketsAndCustomers()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var key = await CreateKeyAsync(admin, "tickets:write", "customers:write", "customers:read");
        var api = WithKey(factory.CreateClient(), key.Key);

        var customer = await api.PostAsJsonAsync("/api/v1/customers", new { name = "API Customer" });
        Assert.Equal(HttpStatusCode.Created, customer.StatusCode);
        var customerId = (await customer.Content.ReadFromJsonAsync<JsonItem>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync($"/api/v1/customers/{customerId}")).StatusCode);

        var ticket = await api.PostAsJsonAsync("/api/v1/tickets", new { customerId, subject = "Created by API" });
        Assert.Equal(HttpStatusCode.Created, ticket.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonAsync("/api/v1/tickets", new { customerId })).StatusCode);
    }

    [Fact]
    public async Task RevokedMissingOrUnknownKey_Returns401()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var key = await CreateKeyAsync(admin, "tickets:read");
        Assert.Equal(HttpStatusCode.OK, (await WithKey(factory.CreateClient(), key.Key).GetAsync("/api/v1/tickets")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/api-keys/{key.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(factory.CreateClient(), key.Key).GetAsync("/api/v1/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(factory.CreateClient(), null).GetAsync("/api/v1/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(factory.CreateClient(), "crm_nope").GetAsync("/api/v1/tickets")).StatusCode);
        // A staff JWT is not an API key.
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/tickets")).StatusCode);
    }

    [Fact]
    public async Task ExceedingTheRateLimit_Returns429_PerKey()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:Api:RequestsPerMinute"] = "3" })));
        var admin = limited.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.LoginAsync());
        var first = await CreateKeyAsync(admin, "tickets:read");
        var second = await CreateKeyAsync(admin, "tickets:read");
        var api = WithKey(limited.CreateClient(), first.Key);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/tickets")).StatusCode);
        }

        var rejected = await api.GetAsync("/api/v1/tickets");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        // Another key has its own window.
        Assert.Equal(HttpStatusCode.OK, (await WithKey(limited.CreateClient(), second.Key).GetAsync("/api/v1/tickets")).StatusCode);
    }

    [Fact]
    public async Task PublicApi_IsDocumentedInSwagger()
    {
        var client = factory.CreateClient();

        var document = await client.GetAsync("/openapi/public-v1.json");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        var json = await document.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/tickets", json);
        Assert.Contains("X-Api-Key", json);
        Assert.DoesNotContain("/api/auth/login", json);

        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }
}
