using System.Net.Http.Headers;
using System.Text.Json;
using Crm.Application.Integrations;
using Microsoft.Extensions.Configuration;

namespace Crm.Infrastructure.Integrations;

/// <summary>
/// A generic REST client for the ERP (CRM-60; the product is not decided). Settings, read at call time from
/// user-secrets / environment variables: <c>Integrations:Erp:BaseUrl</c>, <c>Integrations:Erp:ApiKey</c> (sent as a Bearer
/// token), <c>Integrations:Erp:TimeoutSeconds</c> (default 10). Calls <c>GET {BaseUrl}/customers/{id}/orders?limit=n</c> and
/// <c>.../invoices?limit=n</c>, each answering a JSON array (camelCase fields of <see cref="ErpOrder"/> / <see cref="ErpInvoice"/>).
/// Only this class changes once the real ERP is known.
/// </summary>
public sealed class HttpErpClient(HttpClient client, IConfiguration configuration) : IErpClient
{
    public async Task<ErpCustomerData> GetCustomerDataAsync(string erpCustomerId, int limit, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Integrations:Erp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var root))
        {
            throw new ErpNotConfiguredException();
        }

        var seconds = configuration.GetValue("Integrations:Erp:TimeoutSeconds", 10);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        var apiKey = configuration["Integrations:Erp:ApiKey"];
        var path = $"customers/{Uri.EscapeDataString(erpCustomerId)}";

        try
        {
            var orders = await GetAsync<ErpOrder>(new Uri(root, $"{path}/orders?limit={limit}"), apiKey, timeout.Token);
            var invoices = await GetAsync<ErpInvoice>(new Uri(root, $"{path}/invoices?limit={limit}"), apiKey, timeout.Token);
            return new ErpCustomerData(orders, invoices);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ErpUnavailableException("The ERP did not answer in time.");
        }
        catch (HttpRequestException exception)
        {
            throw new ErpUnavailableException($"The ERP could not be reached: {exception.Message}", exception);
        }
        catch (JsonException exception)
        {
            throw new ErpUnavailableException("The ERP answered with data the CRM cannot read.", exception);
        }
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(Uri uri, string? apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ErpUnavailableException($"The ERP answered {(int)response.StatusCode} {response.ReasonPhrase}".Trim());
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<T>>(body, JsonSerializerOptions.Web, cancellationToken) ?? [];
    }
}
