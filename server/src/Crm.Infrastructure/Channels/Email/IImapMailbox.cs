using Crm.Application.Channels;
using MimeKit;

namespace Crm.Infrastructure.Channels.Email;

/// <summary>The support mailbox (IMAP). Wraps MailKit's <c>ImapClient</c> so tests never open a connection.</summary>
public interface IImapMailbox
{
    /// <summary>
    /// Opens the folder and passes each unseen message (oldest first, at most <paramref name="max"/>) to
    /// <paramref name="handle"/>; a message is marked seen only when the handler returns true. Returns how many were read.
    /// </summary>
    Task<int> ReadUnseenAsync(
        EmailChannelOptions.ImapSettings settings, int max, Func<MimeMessage, CancellationToken, Task<bool>> handle,
        CancellationToken cancellationToken);
}
