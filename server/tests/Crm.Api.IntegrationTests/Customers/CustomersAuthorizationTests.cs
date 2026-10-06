using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomersAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Every /api/customers endpoint (method, path). The id does not need to exist: authorization runs first.</summary>
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "GET", "/api/customers" },
        { "GET", $"/api/customers/{Guid.Empty}" },
        { "POST", "/api/customers" },
        { "PUT", $"/api/customers/{Guid.Empty}" },
        { "DELETE", $"/api/customers/{Guid.Empty}" },
        { "GET", "/api/customers/lookup?phone=%2B966501234567" },
        { "POST", $"/api/customers/{Guid.Empty}/contacts" },
        { "POST", $"/api/customers/{Guid.Empty}/contacts/{Guid.Empty}/primary" },
        { "DELETE", $"/api/customers/{Guid.Empty}/contacts/{Guid.Empty}" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        // A valid body, so a missing authorization check would show up as 201/404 instead of 401/403.
        Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "Blocked customer" }) : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CustomersApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CustomersApi_ForAUserWithoutCustomerPermissions_Returns403(string method, string path)
    {
        // A signed-in user without any role has no permission at all.
        var email = $"norole-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword);
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.Agent)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Admin)]
    public async Task EveryStaffRole_CanCreateAndListCustomers(string role)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var created = await client.PostAsJsonAsync("/api/customers", new { name = $"Created by {role}" });
        var list = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public void CustomerEndpoints_NeedViewToRead_AndManageToWrite()
    {
        // Route patterns as ASP.NET Core stores them: MapGet("") on the group is "/api/customers/".
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/customers", StringComparison.Ordinal) == true)
            .ToDictionary(
                e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}",
                e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).Order().ToArray());

        string[] read = [Permissions.CustomersView];
        string[] write = [Permissions.CustomersManage, Permissions.CustomersView];
        Assert.Equal(read, policies["GET /api/customers/"]);
        Assert.Equal(read, policies["GET /api/customers/{id:guid}"]);
        Assert.Equal(write, policies["POST /api/customers/"]);
        Assert.Equal(write, policies["PUT /api/customers/{id:guid}"]);
        Assert.Equal(write, policies["DELETE /api/customers/{id:guid}"]);
        Assert.Equal(read, policies["GET /api/customers/lookup"]);
        Assert.Equal(write, policies["POST /api/customers/{id:guid}/contacts"]);
        Assert.Equal(write, policies["POST /api/customers/{id:guid}/contacts/{contactId:guid}/primary"]);
        Assert.Equal(write, policies["DELETE /api/customers/{id:guid}/contacts/{contactId:guid}"]);
        Assert.Equal(9, policies.Count);
    }
}
