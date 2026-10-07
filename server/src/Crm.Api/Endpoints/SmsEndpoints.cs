using Crm.Application.Auth;
using Crm.Application.Channels.Sms;
using Crm.Domain.Channels;
using Microsoft.AspNetCore.Http.Extensions;

namespace Crm.Api.Endpoints;

/// <summary>SMS channel (CRM-57): the provider's webhooks (anonymous, signature-checked) and the segment calculator for the reply box.</summary>
public static class SmsEndpoints
{
    public const int MaxBodyBytes = 64 * 1024;

    public sealed record SegmentsRequest(string? Text);

    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var hooks = app.MapGroup("/api/webhooks/sms").AllowAnonymous();

        hooks.MapPost("", async (HttpRequest request, ISmsWebhookService webhook, SmsChannelOptions options, CancellationToken cancellationToken) =>
            {
                var form = await ReadFormAsync(request, cancellationToken);
                if (form is null)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                await webhook.HandleInboundAsync(SignedUrl(request, options), form, request.Headers[TwilioSignature.HeaderName], cancellationToken);
                return Results.Content("<Response/>", "text/xml");
            })
            .WithName("ReceiveSmsWebhook");

        hooks.MapPost("/status", async (HttpRequest request, ISmsWebhookService webhook, SmsChannelOptions options, CancellationToken cancellationToken) =>
            {
                var form = await ReadFormAsync(request, cancellationToken);
                if (form is null)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                await webhook.HandleStatusAsync(SignedUrl(request, options), form, request.Headers[TwilioSignature.HeaderName], cancellationToken);
                return Results.Ok();
            })
            .WithName("ReceiveSmsStatusWebhook");

        app.MapPost("/api/channels/sms/segments", (SegmentsRequest request) => Results.Ok(SmsSegments.Analyze(request.Text)))
            .RequireAuthorization(Permissions.TicketsManage)
            .WithName("CalculateSmsSegments");

        return app;
    }

    /// <summary>The URL the provider signed: the configured public address + path, else the address of this request.</summary>
    private static string SignedUrl(HttpRequest request, SmsChannelOptions options) =>
        string.IsNullOrWhiteSpace(options.WebhookBaseUrl)
            ? request.GetDisplayUrl()
            : options.WebhookBaseUrl.TrimEnd('/') + request.Path.Value + request.QueryString.Value;

    private static async Task<Dictionary<string, string>?> ReadFormAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes || !request.HasFormContentType)
        {
            return request.ContentLength > MaxBodyBytes ? null : [];
        }

        var form = await request.ReadFormAsync(cancellationToken);
        return form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }
}
