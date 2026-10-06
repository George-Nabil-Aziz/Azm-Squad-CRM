using MailKit.Security;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Maps the "Security" setting ("StartTls", "SslOnConnect", "Auto", "None") to MailKit's options.</summary>
internal static class MailKitSecurity
{
    public static SecureSocketOptions Parse(string? value, SecureSocketOptions fallback) =>
        Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
