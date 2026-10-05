using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.ErrorHandling;

public class ErrorHandlingTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task InvalidRequest_Returns400ProblemDetailsWithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/_test/errors/validate",
            new { name = "", email = "not-an-email", age = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("email", problem.Errors.Keys);
        Assert.All(problem.Errors.Values, messages => Assert.NotEmpty(messages));
    }

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/_test/errors/validate",
            new { name = "Sara", email = "sara@example.com", age = 30 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_Returns400ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/_test/errors/validate",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MalformedJson_InDevelopment_Returns400ProblemDetails()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.PostAsync("/_test/errors/validate",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnhandledException_InProduction_Returns500ProblemDetailsWithoutStackTrace()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        var response = await client.GetAsync("/_test/errors/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestEndpointsStartupFilter.SecretMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(500, problem!.Status);
        Assert.Null(problem.Detail);
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_IncludesExceptionDetail()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.GetAsync("/_test/errors/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains(TestEndpointsStartupFilter.SecretMessage, problem!.Detail);
    }

    [Theory]
    [InlineData("/_test/errors/not-found", HttpStatusCode.NotFound)]
    [InlineData("/_test/errors/conflict", HttpStatusCode.Conflict)]
    [InlineData("/_test/errors/forbidden", HttpStatusCode.Forbidden)]
    [InlineData("/api/does-not-exist", HttpStatusCode.NotFound)]
    public async Task KnownFailures_ReturnMatchingProblemDetails(string path, HttpStatusCode expected)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expected, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }
}
