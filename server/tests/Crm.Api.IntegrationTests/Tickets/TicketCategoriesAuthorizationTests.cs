using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

public class TicketCategoriesAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "GET", "/api/ticket-categories" },
        { "POST", "/api/ticket-categories" },
        { "PUT", $"/api/ticket-categories/{Guid.Empty}" },
    };

    public static TheoryData<string, string> WriteEndpointsByRole() => new()
    {
        { Roles.Agent, "POST" },
        { Roles.Agent, "PUT" },
        { Roles.Supervisor, "POST" },
        { Roles.Supervisor, "PUT" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        // A valid body, so a missing authorization check would show up as 201/404 instead of 401/403.
        Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = $"Blocked {Guid.NewGuid():N}" }) : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CategoriesApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(WriteEndpointsByRole))]
    public async Task AgentAndSupervisor_CannotCreateOrUpdateCategories(string role, string method)
    {
        var client = await factory.CreateClientWithRoleAsync(role);
        var path = method == "POST" ? "/api/ticket-categories" : $"/api/ticket-categories/{Guid.Empty}";

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.Agent)]
    [InlineData(Roles.Supervisor)]
    public async Task EveryStaffRole_CanReadCategories(string role)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var response = await client.GetAsync("/api/ticket-categories?activeOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void CategoryEndpoints_NeedTicketsViewToRead_AndCategoriesManageToWrite()
    {
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/ticket-categories", StringComparison.Ordinal) == true)
            .ToDictionary(
                e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}",
                e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).Order().ToArray());

        string[] read = [Permissions.TicketsView];
        string[] write = [Permissions.CategoriesManage, Permissions.TicketsView];
        Assert.Equal(read, policies["GET /api/ticket-categories/"]);
        Assert.Equal(write, policies["POST /api/ticket-categories/"]);
        Assert.Equal(write, policies["PUT /api/ticket-categories/{id:guid}"]);
        Assert.Equal(3, policies.Count);
    }
}
