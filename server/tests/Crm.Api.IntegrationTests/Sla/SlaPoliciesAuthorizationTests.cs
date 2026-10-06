using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Sla;

public class SlaPoliciesAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    // A valid body, so a missing authorization check would show up as 200 instead of 401/403.
    private static readonly object ValidBody = new { responseMinutes = 60, resolutionMinutes = 240 };

    [Fact]
    public async Task SlaApi_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var list = await client.GetAsync("/api/sla-policies");
        var update = await client.PutAsJsonAsync("/api/sla-policies/high", ValidBody);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, update.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Agent)]
    public async Task NonSuperAdmin_UpdatingSla_Gets403(string role)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var update = await client.PutAsJsonAsync("/api/sla-policies/high", ValidBody);
        var list = await client.GetAsync("/api/sla-policies");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal("application/problem+json", update.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public void SlaEndpoints_NeedSlaManage()
    {
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/sla-policies", StringComparison.Ordinal) == true)
            .ToDictionary(
                e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}",
                e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ToArray());

        string[] slaManage = [Permissions.SlaManage];
        Assert.Equal(slaManage, policies["GET /api/sla-policies/"]);
        Assert.Equal(slaManage, policies["PUT /api/sla-policies/{priority}"]);
        Assert.Equal(2, policies.Count);
    }
}
