using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Application.Ai;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Ai;

/// <summary>
/// <see cref="IAiTextService"/> over the Claude Messages API (<c>POST {BaseUrl}/v1/messages</c>). Prompts and answers contain
/// customer data, so they are never logged: a failure logs only the HTTP status code.
/// </summary>
public sealed class AnthropicTextService(HttpClient http, AiOptions options, ILogger<AnthropicTextService> logger) : IAiTextService
{
    private const string ApiVersion = "2023-06-01";

    public bool IsConfigured => options.IsConfigured;

    public async Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint())
        {
            Content = JsonContent.Create(new MessagesRequest(
                string.IsNullOrWhiteSpace(options.Model) ? AiOptions.DefaultModel : options.Model,
                request.MaxTokens,
                request.System,
                [new MessageItem("user", request.User)])),
        };
        message.Headers.Add("x-api-key", options.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The AI provider answered with status {StatusCode}.", (int)response.StatusCode);
                throw new AiFailedException($"The AI provider answered with status {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken);
            var text = new StringBuilder();
            foreach (var block in body?.Content ?? [])
            {
                if (block.Type == "text" && block.Text is not null)
                {
                    text.Append(block.Text);
                }
            }

            if (text.Length == 0)
            {
                throw new AiFailedException("The AI provider answered without text.");
            }

            return text.ToString();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller gave up: not an AI failure
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            logger.LogWarning("The AI request failed ({ExceptionType}).", exception.GetType().Name);
            throw new AiFailedException("The AI request failed.", exception);
        }
    }

    private Uri Endpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? AiOptions.DefaultBaseUrl : options.BaseUrl;
        return new Uri(baseUrl.TrimEnd('/') + "/v1/messages");
    }

    private sealed record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] MessageItem[] Messages);

    private sealed record MessageItem(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record MessagesResponse([property: JsonPropertyName("content")] ContentBlock[]? Content);

    private sealed record ContentBlock(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}
