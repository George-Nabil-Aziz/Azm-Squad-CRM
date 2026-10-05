using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests;

public class HealthEndpointTests(CrmApiFactory factory)
    : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task GetHealth_Returns200WithStatusOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body?.Status);
    }

    private sealed record HealthBody(string Status);
}
