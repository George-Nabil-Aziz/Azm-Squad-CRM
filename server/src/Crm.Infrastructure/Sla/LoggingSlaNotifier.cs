using Crm.Application.Sla;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Sla;

/// <summary>Default <see cref="ISlaNotifier"/>: writes the notice to the log (e-mail / SignalR delivery comes later).</summary>
public sealed partial class LoggingSlaNotifier(ILogger<LoggingSlaNotifier> logger) : ISlaNotifier
{
    public Task NotifyAsync(SlaNotice notice, CancellationToken cancellationToken)
    {
        Log(notice.Type.ToString(), notice.TicketId, notice.Level, notice.RecipientUserId?.ToString() ?? notice.RecipientRole);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SLA notice {Type} for ticket {TicketId} (level {Level}) to {Recipient}")]
    private partial void Log(string type, Guid ticketId, int level, string? recipient);
}
