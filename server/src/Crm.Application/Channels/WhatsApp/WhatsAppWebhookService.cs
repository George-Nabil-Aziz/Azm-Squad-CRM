using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Exceptions;

namespace Crm.Application.Channels.WhatsApp;

public sealed class WhatsAppWebhookService(WhatsAppChannelOptions options, IInboundMessageProcessor processor)
    : IWhatsAppWebhookService
{
    public string VerifySubscription(string? mode, string? verifyToken, string? challenge)
    {
        if (mode != "subscribe" || string.IsNullOrEmpty(challenge) || !TokenMatches(verifyToken))
        {
            throw new ForbiddenException(ChannelText.WebhookVerifyTokenInvalid);
        }

        return challenge;
    }

    public async Task HandleAsync(byte[] body, string? signature, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!WhatsAppSignature.IsValid(body, signature, options.AppSecret))
        {
            throw new UnauthorizedException(ChannelText.WebhookSignatureInvalid);
        }

        var payload = WhatsAppWebhookParser.Parse(body);
        foreach (var message in payload.Messages)
        {
            await processor.ProcessAsync(message, cancellationToken);
        }
    }

    private bool TokenMatches(string? verifyToken) =>
        !string.IsNullOrEmpty(options.VerifyToken) && verifyToken is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(verifyToken), Encoding.UTF8.GetBytes(options.VerifyToken));
}
