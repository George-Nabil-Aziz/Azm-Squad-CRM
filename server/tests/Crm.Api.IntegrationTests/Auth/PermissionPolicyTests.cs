using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Auth;

public partial class PermissionPolicyTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Endpoints any signed-in user may call (no permission needed). Keep this list short.</summary>
    private static readonly string[] SignedInOnlyEndpoints = ["/api/auth/me"];

    private List<RouteEndpoint> ProtectedApiEndpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true
                        && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();

    [Fact]
    public void EveryProtectedApiEndpoint_RequiresAKnownPermission()
    {
        var endpoints = ProtectedApiEndpoints();

        Assert.NotEmpty(endpoints);
        var withoutPermission = endpoints
            .Where(e => !SignedInOnlyEndpoints.Contains(e.RoutePattern.RawText))
            .Where(e => !e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(data => data.Policy is not null && Permissions.All.Contains(data.Policy)))
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.Empty(withoutPermission);
    }

    [Fact]
    public async Task SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint()
    {
        var superAdmin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var requests = ProtectedApiEndpoints()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (method, path: ConcretePath(e.RoutePattern.RawText!))))
            .ToList();

        Assert.NotEmpty(requests);
        foreach (var (method, path) in requests)
        {
            // Empty JSON body: validation may answer 400 and unknown ids 404, but never 401/403.
            var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
            };
            var response = await superAdmin.SendAsync(request);

            Assert.True(
                response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized),
                $"{method} {path} answered {(int)response.StatusCode} to a SuperAdmin.");
        }
    }

    [Theory]
    [InlineData(Permissions.CategoriesManage)]
    [InlineData(Permissions.SlaManage)]
    [InlineData(Permissions.ChannelsManage)]
    [InlineData(Permissions.UsersManage)]
    public async Task AdminSettingsEndpoint_AsAgent_Returns403(string permission)
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync(TestEndpointsStartupFilter.PermissionPath(permission));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.SuperAdmin, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Supervisor, HttpStatusCode.Forbidden)]
    public async Task SlaSettings_OnlySuperAdmin(string role, HttpStatusCode expected)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var response = await client.GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.SlaManage));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Supervisor, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.OK)]
    [InlineData(Roles.Agent, HttpStatusCode.Forbidden)]
    public async Task AssignTickets_SupervisorAndAboveOnly(string role, HttpStatusCode expected)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var response = await client.GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.TicketsAssign));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task PermissionEndpoint_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(TestEndpointsStartupFilter.PermissionPath(Permissions.TicketsView));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>"/api/users/{id:guid}" → "/api/users/&lt;new guid&gt;"; other parameters become "1".</summary>
    private static string ConcretePath(string pattern) =>
        RouteParameter().Replace(pattern, match =>
            match.Groups["constraint"].Value.Contains("guid", StringComparison.Ordinal) ? Guid.NewGuid().ToString() : "1");

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(?<constraint>[^}]*)\}")]
    private static partial Regex RouteParameter();
}
