using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

public class TicketsAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Every /api/tickets endpoint (method, path). Ids need not exist: authorization runs first.</summary>
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "POST", "/api/tickets" },
        { "GET", $"/api/tickets/{Guid.Empty}" },
        { "GET", "/api/tickets" },
        { "GET", "/api/tickets/assignees" },
        { "GET", "/api/tickets/mine" },
        { "GET", $"/api/tickets/{Guid.Empty}/customer-context" },
        { "GET", $"/api/tickets/{Guid.Empty}/messages" },
        { "POST", $"/api/tickets/{Guid.Empty}/messages" },
        { "POST", $"/api/tickets/{Guid.Empty}/assign" },
        { "PUT", $"/api/tickets/{Guid.Empty}/status" },
        { "GET", $"/api/tickets/{Guid.Empty}/history" },
        { "PUT", $"/api/tickets/{Guid.Empty}/category" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        Content = method is "POST" ? JsonContent.Create(new { customerId = Guid.NewGuid(), subject = "Blocked" }) : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task TicketsApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task TicketsApi_ForAUserWithoutTicketPermissions_Returns403(string method, string path)
    {
        var email = $"norole-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword);
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void TicketEndpoints_NeedViewToRead_AndManageToCreate()
    {
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/tickets", StringComparison.Ordinal) == true)
            .ToDictionary(
                e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}",
                e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).Order().ToArray());

        string[] read = [Permissions.TicketsView];
        string[] write = [Permissions.TicketsManage, Permissions.TicketsView];
        Assert.Equal(write, policies["POST /api/tickets/"]);
        Assert.Equal(write, policies["PUT /api/tickets/{id:guid}/priority"]);
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}"]);
        Assert.Equal(read, policies["GET /api/tickets/"]);
        Assert.Equal(read, policies["GET /api/tickets/assignees"]);
        Assert.Equal(read, policies["GET /api/tickets/mine"]);
        string[] withCustomers = [Permissions.CustomersView, Permissions.TicketsView];
        Assert.Equal(withCustomers, policies["GET /api/tickets/{id:guid}/customer-context"]);
        Assert.Equal(write, policies["POST /api/tickets/{id:guid}/messages/"]);
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/messages/"]);
        Assert.Equal(write, policies["POST /api/tickets/{id:guid}/assign"]);
        Assert.Equal(write, policies["PUT /api/tickets/{id:guid}/status"]);
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/history"]);
        Assert.Equal(write, policies["PUT /api/tickets/{id:guid}/category"]);
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/articles/"]); // CRM-39
        string[] link = [Permissions.KbView, Permissions.TicketsManage, Permissions.TicketsView];
        Assert.Equal(link, policies["POST /api/tickets/{id:guid}/articles/"]);
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/attachments/"]); // CRM-41
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/attachments/{attachmentId:guid}"]);
        Assert.Equal(write, policies["PUT /api/tickets/{id:guid}/department"]); // CRM-61
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/ai-summary"]); // CRM-50
        Assert.Equal(write, policies["POST /api/tickets/{id:guid}/ai-summary"]);
        Assert.Equal(write, policies["POST /api/tickets/{id:guid}/ai-reply-draft"]); // CRM-51
        Assert.Equal(read, policies["GET /api/tickets/{id:guid}/ai-classification"]); // CRM-52
        string[] suggest = [Permissions.KbView, Permissions.TicketsView];
        Assert.Equal(suggest, policies["GET /api/tickets/{id:guid}/ai-suggestions"]); // CRM-53
        Assert.Equal(link, policies["PUT /api/tickets/{id:guid}/ai-suggestions/{articleId:guid}/feedback"]);
        Assert.Equal(24, policies.Count);
    }
}
