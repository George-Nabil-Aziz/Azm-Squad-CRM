using System.Security.Cryptography;
using System.Text;

namespace Crm.Application.Channels.WhatsApp;

/// <summary>
/// Webhook signature of the WhatsApp Cloud API: header <c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c>, the HMAC-SHA256 of
/// the raw request body with the app secret.
/// </summary>
public static class WhatsAppSignature
{
    public const string HeaderName = "X-Hub-Signature-256";
    private const string Prefix = "sha256=";

    public static string Compute(ReadOnlySpan<byte> body, string appSecret) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body));

    /// <summary>True only for a well-formed header that matches (constant-time compare); false without a secret.</summary>
    public static bool IsValid(ReadOnlySpan<byte> body, string? header, string? appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(header)
            || !header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] received;
        try
        {
            received = Convert.FromHexString(header.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return received.Length == expected.Length && CryptographicOperations.FixedTimeEquals(received, expected);
    }
}
