namespace Crm.Application.Channels;

/// <summary>
/// Settings section <c>Channels:Email</c>. Passwords only in user-secrets / environment variables
/// (<c>Channels__Email__Smtp__Password</c>), never in a committed appsettings file. Missing values never stop the app:
/// the email channel then reports "not configured".
/// </summary>
public sealed class EmailChannelOptions
{
    public const string SectionName = "Channels:Email";

    /// <summary>Sender address of replies (also the support mailbox customers write to).</summary>
    public string? FromAddress { get; set; }

    /// <summary>Display name of the sender, e.g. "AZM Squad Support".</summary>
    public string? FromName { get; set; }

    public SmtpSettings Smtp { get; set; } = new();

    public ImapSettings Imap { get; set; } = new();

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(Smtp.Host) && !string.IsNullOrWhiteSpace(FromAddress);

    public bool IsImapConfigured => !string.IsNullOrWhiteSpace(Imap.Host) && !string.IsNullOrWhiteSpace(Imap.UserName);

    public sealed class SmtpSettings
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 587;

        /// <summary>"StartTls" (default), "SslOnConnect", "Auto" or "None".</summary>
        public string Security { get; set; } = "StartTls";

        /// <summary>Optional: no authentication when empty.</summary>
        public string? UserName { get; set; }

        public string? Password { get; set; }
    }

    public sealed class ImapSettings
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 993;

        /// <summary>"SslOnConnect" (default), "StartTls", "Auto" or "None".</summary>
        public string Security { get; set; } = "SslOnConnect";

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string Folder { get; set; } = "INBOX";

        /// <summary>At most this many unseen emails are read per poll.</summary>
        public int BatchSize { get; set; } = 50;
    }
}
