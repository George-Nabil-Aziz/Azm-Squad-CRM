using System.Net.Http.Headers;
using System.Text;
using Crm.Application.Integrations;

namespace Crm.Infrastructure.Integrations;

/// <summary>POSTs a webhook delivery (JSON) with a 10 second timeout; any 2xx answer is a success.</summary>
public sealed class HttpWebhookSender(HttpClient client) : IWebhookSender
{
    public async Task<WebhookSendResult> SendAsync(
        string url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var code = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new WebhookSendResult(true, code, null)
                : new WebhookSendResult(false, code, $"The receiver answered {code} {response.ReasonPhrase}".Trim());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendResult(false, null, exception is TaskCanceledException ? "The receiver did not answer in time." : exception.Message);
        }
    }
}
