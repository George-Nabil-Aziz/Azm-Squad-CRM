using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.IntegrationTests.Auth;

public class ProtectedEndpointTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string MePath = "/api/auth/me";

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser()
    {
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeBody>();
        Assert.Equal(CrmApiFactory.SuperAdminEmail, me!.Email);
        Assert.Equal("System Administrator", me.FullName);
        Assert.Equal(["SuperAdmin"], me.Roles);
        Assert.NotEqual(Guid.Empty, me.Id);
    }

    [Fact]
    public async Task Me_AsSuperAdmin_ReturnsEveryPermission()
    {
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var me = await client.GetFromJsonAsync<MeBody>(MePath);

        Assert.Equal(Permissions.All, me!.Permissions);
    }

    [Fact]
    public async Task Me_AsAgent_ReturnsOnlyTheAgentPermissions()
    {
        var client = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var me = await client.GetFromJsonAsync<MeBody>(MePath);

        Assert.Equal(["customers.view", "customers.manage", "tickets.view", "tickets.manage", "notifications.view", "tasks.manage", "kb.view", "chat.handle"], me!.Permissions);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenSignedByAnotherKey_Returns401()
    {
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Crm.Api",
            Audience = "Crm.Client",
            Expires = DateTime.UtcNow.AddMinutes(30),
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "SuperAdmin" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-that-is-long-enough-0123456789")),
                SecurityAlgorithms.HmacSha256),
        });
        var client = factory.CreateAuthenticatedClient(forged);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMalformedAuthorizationHeader_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(endpoints);
        var unprotected = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null
                        && e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.Empty(unprotected);
    }

    private sealed record MeBody(Guid Id, string Email, string FullName, string[] Roles, string[] Permissions);
}
