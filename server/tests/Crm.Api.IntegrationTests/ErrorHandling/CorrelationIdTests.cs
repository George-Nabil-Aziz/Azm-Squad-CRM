using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.ErrorHandling;

public class CorrelationIdTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task UnhandledException_IsLoggedWithCorrelationId()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/_test/errors/unhandled");
        request.Headers.Add(Header, "test-corr-500");

        var response = await client.SendAsync(request);

        Assert.Equal("test-corr-500", response.Headers.GetValues(Header).Single());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("test-corr-500", problem!.Extensions["correlationId"]?.ToString());
        var entry = Assert.Single(factory.Logs.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("test-corr-500"));
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(entry.Scopes, scope => scope is IEnumerable<KeyValuePair<string, object>> pairs
            && pairs.Any(p => p.Key == "CorrelationId" && (string)p.Value == "test-corr-500"));
    }

    [Fact]
    public async Task MissingCorrelationHeader_GeneratesOne()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(Header).Single());
    }

    [Fact]
    public async Task InvalidCorrelationHeader_IsReplacedByAGeneratedOne()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.TryAddWithoutValidation(Header, "bad id with spaces");

        var response = await client.SendAsync(request);

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(Header).Single());
    }
}
