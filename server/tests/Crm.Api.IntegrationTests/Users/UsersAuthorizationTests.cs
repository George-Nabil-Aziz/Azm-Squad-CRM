using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Users;

public class UsersAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Every /api/users endpoint (method, path). The ids do not need to exist: authorization runs first.</summary>
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "GET", "/api/users" },
        { "GET", $"/api/users/{Guid.Empty}" },
        { "POST", "/api/users" },
        { "PUT", $"/api/users/{Guid.Empty}" },
        { "POST", $"/api/users/{Guid.Empty}/deactivate" },
        { "POST", $"/api/users/{Guid.Empty}/reactivate" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        // A valid body, so a missing authorization check would show up as 201/200 instead of 403.
        Content = method is "POST" or "PUT"
            ? JsonContent.Create(new
            {
                email = $"blocked-{Guid.NewGuid():N}@crm.local",
                fullName = "Blocked",
                password = "Blocked#123",
                roles = new[] { "Agent" },
            })
            : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UsersApi_AsAgent_Returns403(string method, string path)
    {
        var agent = await factory.CreateClientWithRoleAsync("Agent");

        var response = await agent.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UsersApi_AsSupervisor_Returns403()
    {
        var supervisor = await factory.CreateClientWithRoleAsync("Supervisor");

        var response = await supervisor.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("You do not have permission to perform this action.", problem!.Title);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UsersApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task UsersApi_AsAdminOrSuperAdmin_Returns200(string role)
    {
        var admin = await factory.CreateClientWithRoleAsync(role);

        var response = await admin.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
