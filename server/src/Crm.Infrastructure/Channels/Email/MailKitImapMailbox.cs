using Crm.Application.Channels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>Real IMAP mailbox: one connection per poll; unseen messages are flagged \Seen after they were handled.</summary>
public sealed class MailKitImapMailbox : IImapMailbox
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public async Task<int> ReadUnseenAsync(
        EmailChannelOptions.ImapSettings settings, int max, Func<MimeMessage, CancellationToken, Task<bool>> handle,
        CancellationToken cancellationToken)
    {
        var host = settings.Host ?? throw new InvalidOperationException("Channels:Email:Imap:Host is not configured.");
        using var client = new ImapClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(host, settings.Port, MailKitSecurity.Parse(settings.Security, SecureSocketOptions.SslOnConnect), cancellationToken);
        await client.AuthenticateAsync(settings.UserName ?? string.Empty, settings.Password ?? string.Empty, cancellationToken);

        var folder = await client.GetFolderAsync(settings.Folder, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        var unseen = (await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken)).Take(Math.Max(1, max)).ToList();

        foreach (var uid in unseen)
        {
            var message = await folder.GetMessageAsync(uid, cancellationToken);
            if (await handle(message, cancellationToken))
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);
            }
        }

        await client.DisconnectAsync(quit: true, cancellationToken);
        return unseen.Count;
    }
}
