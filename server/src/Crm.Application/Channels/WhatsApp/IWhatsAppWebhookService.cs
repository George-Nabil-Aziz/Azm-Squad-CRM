namespace Crm.Application.Channels.WhatsApp;

/// <summary>The WhatsApp Cloud API webhook (anonymous endpoint, protected by the verify token and the signature).</summary>
public interface IWhatsAppWebhookService
{
    /// <summary>Subscription check (GET): the challenge when mode is "subscribe" and the token matches; else <c>ForbiddenException</c>.</summary>
    string VerifySubscription(string? mode, string? verifyToken, string? challenge);

    /// <summary>
    /// Notification (POST): checks the signature over the raw body (<c>UnauthorizedException</c> → 401), then takes in
    /// the customer messages and applies delivery statuses.
    /// </summary>
    Task HandleAsync(byte[] body, string? signature, CancellationToken cancellationToken);
}
