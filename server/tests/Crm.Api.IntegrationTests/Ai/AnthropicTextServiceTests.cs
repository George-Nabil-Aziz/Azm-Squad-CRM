using System.Net;
using System.Text;
using System.Text.Json;
using Crm.Application.Ai;
using Crm.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crm.Api.IntegrationTests.Ai;

public class AnthropicTextServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static AnthropicTextService Create(StubHandler handler, AiOptions? options = null) =>
        new(new HttpClient(handler), options ?? new AiOptions { ApiKey = "sk-test-key" }, NullLogger<AnthropicTextService>.Instance);

    [Fact]
    public async Task SendsAMessagesRequest_WithKeyModelAndPrompt_AndReturnsTheText()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"content":[{"type":"text","text":"Hello "},{"type":"text","text":"world"}]}"""));

        var answer = await Create(handler).CompleteAsync(new AiRequest("Be brief.", "Say hi", 300), CancellationToken.None);

        Assert.Equal("Hello world", answer);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", request.RequestUri!.ToString());
        Assert.Equal("sk-test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
        using var json = JsonDocument.Parse(body);
        Assert.Equal(AiOptions.DefaultModel, json.RootElement.GetProperty("model").GetString());
        Assert.Equal(300, json.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("Be brief.", json.RootElement.GetProperty("system").GetString());
        var message = json.RootElement.GetProperty("messages")[0];
        Assert.Equal(("user", "Say hi"), (message.GetProperty("role").GetString(), message.GetProperty("content").GetString()));
    }

    [Fact]
    public async Task UsesTheConfiguredModelAndBaseUrl()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"content":[{"type":"text","text":"ok"}]}"""));

        await Create(handler, new AiOptions { ApiKey = "k", Model = "claude-test", BaseUrl = "https://proxy.example/" })
            .CompleteAsync(new AiRequest("s", "u"), CancellationToken.None);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("https://proxy.example/v1/messages", request.RequestUri!.ToString());
        Assert.Contains("claude-test", body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ANonSuccessAnswer_IsAnAiFailure(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Json(status, """{"type":"error","error":{"type":"x","message":"secret customer data"}}"""));

        var failure = await Assert.ThrowsAsync<AiFailedException>(() =>
            Create(handler).CompleteAsync(new AiRequest("s", "u"), CancellationToken.None));

        Assert.DoesNotContain("secret customer data", failure.Message);
        Assert.Contains(((int)status).ToString(), failure.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"content":[]}""")]
    [InlineData("""{"content":[{"type":"tool_use"}]}""")]
    public async Task AnAnswerWithoutText_IsAnAiFailure(string json)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, json));

        await Assert.ThrowsAsync<AiFailedException>(() => Create(handler).CompleteAsync(new AiRequest("s", "u"), CancellationToken.None));
    }

    [Fact]
    public async Task ATimeoutOrNetworkError_IsAnAiFailure()
    {
        await Assert.ThrowsAsync<AiFailedException>(() => Create(new StubHandler(_ => throw new TaskCanceledException("timeout")))
            .CompleteAsync(new AiRequest("s", "u"), CancellationToken.None));
        await Assert.ThrowsAsync<AiFailedException>(() => Create(new StubHandler(_ => throw new HttpRequestException("down")))
            .CompleteAsync(new AiRequest("s", "u"), CancellationToken.None));
    }

    [Fact]
    public async Task ACancelledCaller_IsNotAnAiFailure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(new StubHandler(_ => throw new TaskCanceledException()))
            .CompleteAsync(new AiRequest("s", "u"), cts.Token));
    }

    [Fact]
    public async Task WithoutAKey_NothingIsSent_AndItIsNotConfigured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var service = Create(handler, new AiOptions { ApiKey = " " });

        Assert.False(service.IsConfigured);
        await Assert.ThrowsAsync<AiNotConfiguredException>(() => service.CompleteAsync(new AiRequest("s", "u"), CancellationToken.None));
        Assert.Empty(handler.Calls);
    }
}
