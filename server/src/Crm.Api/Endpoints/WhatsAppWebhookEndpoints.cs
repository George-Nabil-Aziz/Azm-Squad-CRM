using Crm.Application.Channels.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Endpoints;

public static class WhatsAppWebhookEndpoints
{
    /// <summary>Largest webhook body read (Meta batches a few messages; bigger bodies are refused).</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // Called by Meta, not by users: anonymous, protected by the verify token (GET) and the signature (POST).
        var group = app.MapGroup("/api/webhooks/whatsapp").AllowAnonymous();

        group.MapGet("", ([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? verifyToken,
                    [FromQuery(Name = "hub.challenge")] string? challenge, IWhatsAppWebhookService webhook) =>
                Results.Text(webhook.VerifySubscription(mode, verifyToken, challenge), "text/plain"))
            .WithName("VerifyWhatsAppWebhook");

        group.MapPost("", async (HttpRequest request, IWhatsAppWebhookService webhook, CancellationToken cancellationToken) =>
            {
                if (request.ContentLength > MaxBodyBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                // The signature covers the exact raw bytes: read them before any JSON parsing.
                using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer, cancellationToken);
                if (buffer.Length > MaxBodyBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                await webhook.HandleAsync(buffer.ToArray(), request.Headers[WhatsAppSignature.HeaderName], cancellationToken);
                return Results.Ok();
            })
            .WithName("ReceiveWhatsAppWebhook");

        return app;
    }
}
